using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.Container.Abstract;
using Soenneker.Cosmos.Repository.Abstract;
using Soenneker.Cosmos.Repository.Abstract.Utils;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
using Soenneker.Utils.BackgroundQueue.Abstract;
using Soenneker.Utils.MemoryStream.Abstract;
using Soenneker.Utils.UserContext.Abstract;
using Soenneker.Utils.PooledStringBuilders;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

/// <inheritdoc cref="ICosmosRepository{TDocument}"/>
public abstract partial class CosmosRepository<TDocument> : ICosmosRepository<TDocument>, ICosmosRepositoryContext where TDocument : Document
{
    private readonly ICosmosContainerUtil _cosmosContainerUtil;

    /// <summary>
    /// Audit container that will store audit log for all entities.
    /// TODO: Perhaps need to make audit container available...
    /// </summary>
    private ValueTask<Microsoft.Azure.Cosmos.Container> AuditContainer(CancellationToken cancellationToken = default) =>
        _cosmosContainerUtil.Get("audits", cancellationToken);

    /// <summary>
    /// Cosmos DB container
    /// </summary>
    protected ValueTask<Microsoft.Azure.Cosmos.Container> Container(CancellationToken cancellationToken = default) =>
        _cosmosContainerUtil.Get(ContainerName, cancellationToken);

    /// <summary>
    /// Should we create audit records for this repository event?
    /// </summary>
    public virtual bool AuditEnabled => true;

    /// <summary>
    /// Name of the CosmosDB container
    /// </summary>
    public abstract string ContainerName { get; }

    protected ILogger<CosmosRepository<TDocument>> Logger { get; }

    private readonly IUserContext _userContext;
    private readonly IBackgroundQueue _backgroundQueue;
    private readonly IMemoryStreamUtil _memoryStreamUtil;

    private readonly bool _log;
    private readonly bool _auditLog;

    protected CosmosRepository(ICosmosContainerUtil cosmosContainerUtil, IConfiguration config, ILogger<CosmosRepository<TDocument>> logger,
        IUserContext userContext, IBackgroundQueue backgroundQueue, IMemoryStreamUtil memoryStreamUtil)
    {
        _cosmosContainerUtil = cosmosContainerUtil;
        Logger = logger;
        _userContext = userContext;
        _backgroundQueue = backgroundQueue;
        _memoryStreamUtil = memoryStreamUtil;

        _log = config.GetValue<bool>("Azure:Cosmos:Log");
        _auditLog = config.GetValue<bool>("Azure:Cosmos:AuditLog");
    }

    public virtual PartitionKey ResolvePartitionKey(string entityId)
    {
        (string partitionKey, string _) = entityId.ToSplitId();
        return new PartitionKey(partitionKey);
    }

    // TODO: Log response

    private void LogQuery<T>(QueryDefinition queryDefinition, string? methodName)
    {
        if (!_log)
            return;

        string queryText = BuildQueryLogText(queryDefinition);

        Logger.LogDebug("-- COSMOS: {method} ({type}): {query}", methodName, typeof(T).Name, queryText);
    }

    private void LogQuery<T>(IQueryable query, string? methodName)
    {
        if (!_log)
            return;

        var queryText = query.ToString();

        Logger.LogDebug("-- COSMOS: {method} ({type}): {query}", methodName, typeof(T).Name, queryText);
    }

    private static string BuildQueryLogText(QueryDefinition queryDefinition)
    {
        string queryText = queryDefinition.QueryText;

        IReadOnlyList<(string Name, object Value)> parameters = queryDefinition.GetQueryParameters();

        if (parameters.Count == 0)
            return queryText;

        // Build lookup (ordinal is correct for parameter tokens)
        var map = new Dictionary<string, object?>(parameters.Count, StringComparer.Ordinal);

        for (var i = 0; i < parameters.Count; i++)
        {
            (string Name, object Value) p = parameters[i];
            map[p.Name] = p.Value;
        }

        // Rough guess: output usually a bit longer because of quotes/"null"
        var psb = new PooledStringBuilder(queryText.Length + parameters.Count * 8);

        try
        {
            ReadOnlySpan<char> span = queryText.AsSpan();
            ref char r0 = ref MemoryMarshal.GetReference(span);
            int len = span.Length;

            for (var i = 0; i < len; i++)
            {
                char c = Unsafe.Add(ref r0, i);

                // Cosmos parameters typically start with '@'
                if (c != '@')
                {
                    psb.Append(c);
                    continue;
                }

                int start = i;
                int j = i + 1;

                while (j < len)
                {
                    char ch = Unsafe.Add(ref r0, j);
                    if (ch == '_' || char.IsLetterOrDigit(ch))
                    {
                        j++;
                        continue;
                    }

                    break;
                }

                // Just '@' by itself
                if (j == start + 1)
                {
                    psb.Append('@');
                    continue;
                }

                string token = span.Slice(start, j - start).ToString();

                if (map.TryGetValue(token, out object? value))
                {
                    AppendFormattedValue(ref psb, value);
                    i = j - 1; // skip token
                }
                else
                {
                    // no replacement found
                    psb.Append(token);
                    i = j - 1;
                }
            }

            return psb.ToString();
        }
        finally
        {
            psb.Dispose();
        }

        static void AppendFormattedValue(ref PooledStringBuilder psb, object? value)
        {
            if (value is null)
            {
                psb.Append("null");
                return;
            }

            switch (value)
            {
                case string s:
                    psb.Append('"');
                    psb.Append(s);
                    psb.Append('"');
                    return;

                case DateTime dt:
                    psb.Append('"');
                    psb.Append(dt.ToString("O", CultureInfo.InvariantCulture));
                    psb.Append('"');
                    return;

                case DateTimeOffset dto:
                    psb.Append('"');
                    psb.Append(dto.ToString("O", CultureInfo.InvariantCulture));
                    psb.Append('"');
                    return;

                default:
                    var str = Convert.ToString(value);
                    psb.Append(str ?? "null");
                    return;
            }
        }
    }
}
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.Container.Abstract;
using Soenneker.Cosmos.Repository.Abstract;
using Soenneker.Cosmos.Repository.Abstract.Utils;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
using Soenneker.Utils.BackgroundQueue.Abstract;
using Soenneker.Utils.UserContext.Abstract;

namespace Soenneker.Cosmos.Repository;

/// <inheritdoc cref="ICosmosRepository{TDocument}"/>
public abstract partial class CosmosRepository<TDocument> : ICosmosRepository<TDocument>, ICosmosRepositoryContext where TDocument : Document
{
    private readonly ICosmosContainerUtil _cosmosContainerUtil;

    /// <summary>
    /// Audit container that will store audit log for all entities.
    /// TODO: Perhaps need to make audit container available...
    /// </summary>
    private ValueTask<Microsoft.Azure.Cosmos.Container> AuditContainer(CancellationToken cancellationToken = default) => _cosmosContainerUtil.Get("audits", cancellationToken);

    /// <summary>
    /// Cosmos DB container
    /// </summary>
    protected ValueTask<Microsoft.Azure.Cosmos.Container> Container(CancellationToken cancellationToken = default) => _cosmosContainerUtil.Get(ContainerName, cancellationToken);

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

    private readonly bool _log;
    private readonly bool _auditLog;

    private readonly ItemRequestOptions _excludeRequestOptions;
    private readonly QueryRequestOptions _maxOneRequestOptions;

    protected CosmosRepository(ICosmosContainerUtil cosmosContainerUtil, IConfiguration config, ILogger<CosmosRepository<TDocument>> logger,
        IUserContext userContext, IBackgroundQueue backgroundQueue)
    {
        _cosmosContainerUtil = cosmosContainerUtil;
        Logger = logger;
        _userContext = userContext;
        _backgroundQueue = backgroundQueue;

        _excludeRequestOptions = new ItemRequestOptions {EnableContentResponseOnWrite = false};

        _maxOneRequestOptions = new QueryRequestOptions {MaxItemCount = 1};

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

        // Materialize reversed query parameters to avoid deferred execution
        (string Name, object Value)[] queryParameters = queryDefinition.GetQueryParameters().Reverse().ToArray();

        var builder = new StringBuilder(queryText);

        foreach ((string? name, object? value) in queryParameters)
        {
            string outputValue = value switch
            {
                string or DateTime => $"\"{value}\"",
                null => "null", // Explicitly handle null values
                _ => Convert.ToString(value) ?? "null" // Ensure Convert.ToString doesn't return null
            };

            builder.Replace(name, outputValue);
        }

        return builder.ToString();
    }
}
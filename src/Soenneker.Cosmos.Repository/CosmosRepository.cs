using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.Container.Abstract;
using Soenneker.Cosmos.Repository.Abstract;
using Soenneker.Cosmos.Repository.Abstract.Utils;
using Soenneker.Cosmos.Repository.Dtos;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
using Soenneker.Utils.BackgroundQueue.Abstract;
using Soenneker.Utils.MemoryStream.Abstract;
using Soenneker.Utils.UserContext.Abstract;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> : ICosmosRepository<TDocument>, ICosmosRepositoryContext where TDocument : Document
{
    private readonly JsonSerializerContext _jsonContext;


    private const int _documentIdBatchSize = 50;

    private readonly ICosmosContainerUtil _cosmosContainerUtil;

    private ValueTask<Microsoft.Azure.Cosmos.Container> AuditContainer(CancellationToken cancellationToken = default) =>
        _cosmosContainerUtil.Get("audits", cancellationToken);

    /// <summary>
    /// Gets the Cosmos DB container used by this repository.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The repository's Cosmos DB container.</returns>
    protected ValueTask<Microsoft.Azure.Cosmos.Container> Container(CancellationToken cancellationToken = default) =>
        _cosmosContainerUtil.Get(ContainerName, cancellationToken);

    public virtual bool AuditEnabled => true;

    public virtual CosmosReadOptions? DefaultReadOptions => null;

    public virtual CosmosWriteOptions? DefaultWriteOptions => null;

    private void EnsureUnconditionalWriteAllowed(CosmosWriteOptions? writeOptions)
    {
        if (DefaultWriteOptions?.RequireETag == true || writeOptions?.RequireETag == true)
            throw new InvalidOperationException("This operation requires an ETag. Use an IfMatch method or MutateItem.");
    }

    public abstract string ContainerName { get; }

    protected ILogger<CosmosRepository<TDocument>> Logger { get; }

    private readonly IUserContext _userContext;
    private readonly IBackgroundQueue _backgroundQueue;

    private readonly bool _log;
    private readonly bool _auditLog;

    protected CosmosRepository(JsonSerializerContext jsonContext, ICosmosContainerUtil cosmosContainerUtil, IConfiguration config, ILogger<CosmosRepository<TDocument>> logger,
        IUserContext userContext, IBackgroundQueue backgroundQueue, IMemoryStreamUtil memoryStreamUtil)
    {
        _jsonContext = jsonContext ?? throw new System.ArgumentNullException(nameof(jsonContext));
        _cosmosContainerUtil = cosmosContainerUtil;
        Logger = logger;
        _userContext = userContext;
        _backgroundQueue = backgroundQueue;
        _ = memoryStreamUtil;

        _log = config.GetValue<bool>("Azure:Cosmos:Log");
        _auditLog = config.GetValue<bool>("Azure:Cosmos:AuditLog");
    }

    public virtual PartitionKey ResolvePartitionKey(string entityId)
    {
        (Range partition, _) = entityId.ToSplitIdRanges();
        return new PartitionKey(entityId[partition]);
    }

    // TODO: Log response

    private void LogQuery<T>(QueryDefinition queryDefinition, string? methodName)
    {
        if (!_log || !Logger.IsEnabled(LogLevel.Debug))
            return;

        string queryText = BuildQueryLogText(queryDefinition);

        Logger.LogDebug("-- COSMOS: {method} ({type}): {query}", methodName, typeof(T).Name, queryText);
    }

    private void LogQuery<T>(IQueryable query, string? methodName)
    {
        if (!_log || !Logger.IsEnabled(LogLevel.Debug))
            return;

        Logger.LogDebug("-- COSMOS: {method} ({type}): LINQ query", methodName, typeof(T).Name);
    }

    private static string BuildQueryLogText(QueryDefinition queryDefinition) => queryDefinition.QueryText;
}

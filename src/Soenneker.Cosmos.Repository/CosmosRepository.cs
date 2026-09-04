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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

/// <inheritdoc cref="ICosmosRepository{TDocument}" />
public abstract partial class CosmosRepository<TDocument> : ICosmosRepository<TDocument>, ICosmosRepositoryContext where TDocument : Document
{
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

    public abstract string ContainerName { get; }

    protected ILogger<CosmosRepository<TDocument>> Logger { get; }

    private readonly IUserContext _userContext;
    private readonly IBackgroundQueue _backgroundQueue;

    private readonly bool _log;
    private readonly bool _auditLog;

    protected CosmosRepository(ICosmosContainerUtil cosmosContainerUtil, IConfiguration config, ILogger<CosmosRepository<TDocument>> logger,
        IUserContext userContext, IBackgroundQueue backgroundQueue, IMemoryStreamUtil memoryStreamUtil)
    {
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

        Logger.LogDebug("-- COSMOS: {method} ({type}): LINQ query", methodName, typeof(T).Name);
    }

    private static string BuildQueryLogText(QueryDefinition queryDefinition) => queryDefinition.QueryText;
}

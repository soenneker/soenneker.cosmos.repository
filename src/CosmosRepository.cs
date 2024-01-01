using System;
using System.Collections.Generic;
using System.Linq;
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
using Soenneker.Utils.Cancellation.Abstract;
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
    private ValueTask<Microsoft.Azure.Cosmos.Container> AuditContainer => _cosmosContainerUtil.GetContainer("audits");

    /// <summary>
    /// Cosmos DB container
    /// </summary>
    protected ValueTask<Microsoft.Azure.Cosmos.Container> Container => _cosmosContainerUtil.GetContainer(ContainerName);

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
    private readonly ICancellationUtil _cancellationUtil;
    private readonly IBackgroundQueue _backgroundQueue;

    private readonly bool _log;
    private readonly bool _auditLog;

    private readonly ItemRequestOptions _excludeRequestOptions;

    protected CosmosRepository(ICosmosContainerUtil cosmosContainerUtil, IConfiguration config, ILogger<CosmosRepository<TDocument>> logger,
        IUserContext userContext, ICancellationUtil cancellationUtil, IBackgroundQueue backgroundQueue)
    {
        _cosmosContainerUtil = cosmosContainerUtil;
        Logger = logger;
        _userContext = userContext;
        _cancellationUtil = cancellationUtil;
        _backgroundQueue = backgroundQueue;
        
        _excludeRequestOptions = new ItemRequestOptions
        {
            EnableContentResponseOnWrite = false
        };

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

    private void LogQuery<T>(IQueryable queryable, string? methodName)
    {
        if (!_log)
            return;

        var queryText = queryable.ToString();

        Logger.LogDebug("-- COSMOS: {method} ({type}): {query}", methodName, typeof(T).Name, queryText);
    }

    private static string BuildQueryLogText(QueryDefinition queryDefinition)
    {
        string queryText = queryDefinition.QueryText;

        IEnumerable<(string Name, object Value)> queryParameters = queryDefinition.GetQueryParameters().Reverse(); // we need to reverse because in large queries (> 10) we'll overwrite incorrectly

        foreach ((string name, object value) in queryParameters)
        {
            object outputValue = value;

            if (value is string or DateTime)
            {
                outputValue = $"\"{value}\"";
            }

            queryText = queryText.Replace(name, Convert.ToString(outputValue));
        }

        return queryText;
    }
}
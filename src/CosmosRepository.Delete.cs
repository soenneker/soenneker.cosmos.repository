using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Dtos.IdPartitionPair;
using Soenneker.Enums.EventType;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask DeleteItem(string entityId, bool useQueue = false)
    {
        (string partitionKey, string documentId) = entityId.ToSplitId();

        await DeleteItem(documentId, partitionKey, useQueue).NoSync();
    }
    
    public virtual async ValueTask DeleteAll(double? delayMs = null, bool useQueue = false)
    {
        Logger.LogWarning("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name, delayMs.GetValueOrDefault());
        
        List<IdPartitionPair> ids = await GetAllIds().NoSync();

        await DeleteIds(ids, delayMs, useQueue).NoSync();

        Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
    }

    public async ValueTask DeleteItems(IQueryable<TDocument> queryable, double? delayMs = null, bool useQueue = false)
    {
        if (_log)
            Logger.LogWarning("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        List<IdPartitionPair> ids = await GetIds(queryable).NoSync();

        await DeleteIds(ids, delayMs, useQueue).NoSync();
    }

    public virtual async ValueTask DeleteIds(List<IdPartitionPair> ids, double? delayMs = null, bool useQueue = false)
    {
        if (_log)
            Logger.LogDebug("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name, delayMs.GetValueOrDefault());

        TimeSpan? timeSpanDelay = null;

        if (delayMs != null)
            timeSpanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

        foreach (IdPartitionPair id in ids)
        {
            await DeleteItem(id.Id, id.PartitionKey, useQueue).NoSync();

            if (delayMs != null)
                await Task.Delay(timeSpanDelay!.Value).NoSync();
        }

        if (_log)
            Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
    }

    public virtual async ValueTask DeleteItem(string documentId, string partitionKey, bool useQueue = false)
    {
        if (_log)
            Logger.LogDebug("-- COSMOS: {method} ({type}): DocID: {documentId}, PartitionKey: {partitionKey}", MethodUtil.Get(), typeof(TDocument).Name, documentId, partitionKey);

        var partitionKeyObj = new PartitionKey(partitionKey);

        if (useQueue)
        {
            await _backgroundQueue.QueueValueTask(async cancellationToken =>
            {
                Microsoft.Azure.Cosmos.Container container = await Container.NoSync();

                await container.DeleteItemAsync<TDocument>(documentId, partitionKeyObj, _excludeRequestOptions, cancellationToken).NoSync();
            }).NoSync();
        }
        else
        {
            Microsoft.Azure.Cosmos.Container container = await Container.NoSync();

            _ = await container.DeleteItemAsync<TDocument>(documentId, partitionKeyObj, _excludeRequestOptions).NoSync();
        }

        string entityId = documentId.AddPartitionKey(partitionKey);

        if (AuditEnabled)
            await CreateAuditItem(EventType.Delete, entityId).NoSync();
    }
}
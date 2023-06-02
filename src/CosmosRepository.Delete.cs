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
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask DeleteItem(string entityId, bool useQueue = false)
    {
        (string partitionKey, string documentId) = entityId.ToSplitId();

        await DeleteItem(documentId, partitionKey, useQueue);
    }
    
    public virtual async ValueTask DeleteAll(double? delayMs = null, bool useQueue = false)
    {
        Logger.LogWarning("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name, delayMs.GetValueOrDefault());
        
        List<IdPartitionPair> ids = await GetAllIds();

        await DeleteIds(ids, delayMs, useQueue);

        Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
    }

    public async ValueTask DeleteItems(IQueryable<TDocument> queryable, double? delayMs = null, bool useQueue = false)
    {
        if (_log)
            Logger.LogWarning("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        List<IdPartitionPair> ids = await GetIds(queryable);

        await DeleteIds(ids, delayMs, useQueue);
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
            await DeleteItem(id.Id, id.PartitionKey, useQueue);

            if (delayMs != null)
                await Task.Delay(timeSpanDelay!.Value);
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
            await _backgroundQueue.QueueValueTask(async _ =>
            {
                Microsoft.Azure.Cosmos.Container container = await Container;

                await container.DeleteItemAsync<TDocument>(documentId, partitionKeyObj, _excludeRequestOptions, _);
            });
        }
        else
        {
            Microsoft.Azure.Cosmos.Container container = await Container;

            _ = await container.DeleteItemAsync<TDocument>(documentId, partitionKeyObj, _excludeRequestOptions);
        }

        string entityId = documentId.AddPartitionKey(partitionKey);

        if (AuditEnabled)
            await CreateAuditItem(EventType.Delete, entityId);
    }
}
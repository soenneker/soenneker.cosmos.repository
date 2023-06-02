using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public async ValueTask<List<TDocument>> PatchItems(List<TDocument> documents, List<PatchOperation> operations, double? delayMs = null, bool useQueue = false)
    {
        TimeSpan? timespanDelay = null;

        if (delayMs != null)
            timespanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

        foreach (TDocument item in documents)
        {
            _ = await PatchItem(item.Id, operations, useQueue);

            if (delayMs != null)
                await Task.Delay(timespanDelay!.Value);
        }

        return documents;
    }

    public async ValueTask<TDocument?> PatchItem(string id, List<PatchOperation> operations, bool useQueue = false)
    {
        if (_log)
        {
            Logger.LogDebug("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
        }

        (string? partitionKey, string? documentId) = id.ToSplitId();

        TDocument? updatedDocument = null;

        // TODO: we should probably move this to replace
        if (useQueue)
        {
            await _backgroundQueue.QueueValueTask(async _ =>
            {
                Microsoft.Azure.Cosmos.Container container = await Container;

                ItemResponse<TDocument>? response = await container.PatchItemAsync<TDocument>(documentId, new PartitionKey(partitionKey), operations, null, _);
                //Logger.LogInformation(response.RequestCharge.ToString());
            });
        }
        else
        {
            Microsoft.Azure.Cosmos.Container container = await Container;

            ItemResponse<TDocument>? response = await container.PatchItemAsync<TDocument>(documentId, new PartitionKey(partitionKey), operations);
            //Logger.LogInformation(response.RequestCharge.ToString());
            updatedDocument = response.Resource;
        }
        
        return updatedDocument;
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public async ValueTask<List<TDocument>> PatchItems(List<TDocument> documents, List<PatchOperation> operations, double? delayMs = null, bool useQueue = false,
        CancellationToken cancellationToken = default)
    {
        // Precompute delay once
        TimeSpan? timespanDelay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        if (timespanDelay.HasValue)
        {
            foreach (TDocument item in documents)
            {
                await PatchItem(item.Id, operations, useQueue, cancellationToken).NoSync();
                await Task.Delay(timespanDelay.Value, cancellationToken).NoSync();
            }
        }
        else
        {
            foreach (TDocument item in documents)
            {
                await PatchItem(item.Id, operations, useQueue, cancellationToken).NoSync();
            }
        }

        return documents;
    }

    public async ValueTask<TDocument?> PatchItem(string id, List<PatchOperation> operations, bool useQueue = false, CancellationToken cancellationToken = default)
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
            await _backgroundQueue.QueueValueTask(async token =>
            {
                Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

                _ = await container.PatchItemAsync<TDocument>(documentId, new PartitionKey(partitionKey), operations, null, token).NoSync();
                //Logger.LogInformation(response.RequestCharge.ToString());
            }, cancellationToken).NoSync();
        }
        else
        {
            Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

            ItemResponse<TDocument>? response = await container.PatchItemAsync<TDocument>(documentId, new PartitionKey(partitionKey), operations, cancellationToken: cancellationToken).NoSync();
            //Logger.LogInformation(response.RequestCharge.ToString());
            updatedDocument = response.Resource;
        }

        return updatedDocument;
    }
}
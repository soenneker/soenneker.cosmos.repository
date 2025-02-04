using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Enums.EventType;
using Soenneker.Enums.JsonOptions;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Json;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<TDocument> UpdateItem(TDocument item, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
        return UpdateItem(item.Id, item, useQueue, excludeResponse, cancellationToken);
    }

    // Avoids container lookup per item, thus not using UpdateItem
    public async ValueTask<List<TDocument>> UpdateItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        // Fetch the container once
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        TimeSpan? timespanDelay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        for (var i = 0; i < documents.Count; i++)
        {
            TDocument item = documents[i];

            if (_log)
            {
                string? serialized = JsonUtil.Serialize(item, JsonOptionType.Pretty);
                Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
            }

            // Parse ID into partition key and document ID
            (string partitionKey, string documentId) = item.Id.ToSplitId();

            // Precompute request options
            ItemRequestOptions? options = excludeResponse ? ExcludeRequestOptions : null;

            if (useQueue)
            {
                await _backgroundQueue.QueueValueTask(async token =>
                {
                    await container.ReplaceItemAsync(
                        item,
                        documentId,
                        new PartitionKey(partitionKey),
                        options,
                        token).NoSync();

                    if (AuditEnabled)
                        await CreateAuditItem(EventType.Update, item.Id, item, token).NoSync();

                }, cancellationToken).NoSync();
            }
            else
            {
                ItemResponse<TDocument>? response = await container.ReplaceItemAsync(
                    item,
                    documentId,
                    new PartitionKey(partitionKey),
                    options,
                    cancellationToken).NoSync();

                if (AuditEnabled)
                    await CreateAuditItem(EventType.Update, item.Id, item, cancellationToken).NoSync();

                // Update the document in the original list
                documents[i] = response.Resource ?? item;
            }

            if (timespanDelay.HasValue)
                await Task.Delay(timespanDelay.Value, cancellationToken).NoSync();
        }

        return documents;
    }

    public async ValueTask<TDocument> UpdateItem(string id, TDocument item, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
        if (_log)
        {
            string? serialized = JsonUtil.Serialize(item, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        // Parse ID into partition key and document ID
        (string partitionKey, string documentId) = id.ToSplitId();

        // Precompute request options
        ItemRequestOptions? options = excludeResponse ? ExcludeRequestOptions : null;

        // UseQueue Logic
        if (useQueue)
        {
            await _backgroundQueue.QueueValueTask(async token =>
            {
                Microsoft.Azure.Cosmos.Container container = await Container(token).NoSync();

                await container.ReplaceItemAsync(
                    item,
                    documentId,
                    new PartitionKey(partitionKey),
                    options,
                    token).NoSync();

                // Perform audit within the queued task
                if (AuditEnabled)
                    await CreateAuditItem(EventType.Update, id, item, token).NoSync();
            }, cancellationToken).NoSync();

            return item;
        }

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        ItemResponse<TDocument>? response = await container.ReplaceItemAsync(
            item,
            documentId,
            new PartitionKey(partitionKey),
            options,
            cancellationToken).NoSync();

        if (AuditEnabled)
            await CreateAuditItem(EventType.Update, id, item, cancellationToken).NoSync();

        return response.Resource ?? item;
    }
}
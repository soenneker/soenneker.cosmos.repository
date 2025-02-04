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
    public virtual ValueTask<string> AddItem(TDocument document, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
        if (_log)
        {
            string? serialized = JsonUtil.Serialize(document, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {document}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        return InternalAddItem(document, useQueue, excludeResponse, cancellationToken);
    }

    public virtual async ValueTask<List<TDocument>> AddItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        if (_log)
        {
            Logger.LogDebug(
                "-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs",
                MethodUtil.Get(),
                typeof(TDocument).Name,
                delayMs.GetValueOrDefault());
        }

        if (delayMs.HasValue)
        {
            TimeSpan timeSpanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

            foreach (TDocument item in documents)
            {
                item.Id = await InternalAddItem(item, useQueue, excludeResponse, cancellationToken).NoSync();
                await Task.Delay(timeSpanDelay, cancellationToken).NoSync();
            }
        }
        else
        {
            foreach (TDocument item in documents)
            {
                item.Id = await InternalAddItem(item, useQueue, excludeResponse, cancellationToken).NoSync();
            }
        }

        return documents;
    }

    private async ValueTask<string> InternalAddItem(TDocument document, bool useQueue, bool excludeResponse, CancellationToken cancellationToken)
    {
        if (document.PartitionKey.IsNullOrWhiteSpace() || document.DocumentId.IsNullOrWhiteSpace())
            throw new Exception("DocumentId and PartitionKey MUST be present on the object before storing");

        ItemRequestOptions? options = null;

        if (excludeResponse)
            options = _excludeRequestOptions;

        if (useQueue)
        {
            await _backgroundQueue.QueueValueTask(async token =>
            {
                Microsoft.Azure.Cosmos.Container container = await Container(token).NoSync();

                _ = await container.CreateItemAsync(document, new PartitionKey(document.PartitionKey), options, token).NoSync();
            }, cancellationToken).NoSync();
        }
        else
        {
            Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

            _ = await container.CreateItemAsync(document, new PartitionKey(document.PartitionKey), options, cancellationToken).NoSync();
        }

        if (AuditEnabled)
            await CreateAuditItem(EventType.Create, document.Id, document, cancellationToken).NoSync();

        return document.Id;
    }
}
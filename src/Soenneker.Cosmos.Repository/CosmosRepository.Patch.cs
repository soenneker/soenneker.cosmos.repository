using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Cosmos.Repository.Dtos;
using Soenneker.Enums.CrudEventTypes;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Delay;
using Soenneker.Utils.Method;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<CosmosItem<TDocument>> PatchItemIfMatch(CosmosItem<TDocument> item, List<PatchOperation> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return PatchItemIfMatch(GetRequiredId(item.Document), operations, item.ETag, cancellationToken);
    }

    public async ValueTask<List<TDocument>> PatchItems(List<TDocument> documents, List<PatchOperation> operations, double? delayMs = null,
        bool useQueue = false, CancellationToken cancellationToken = default)
    {
        return await PatchItemsCore(documents, operations, delayMs, useQueue, cancellationToken).NoSync();
    }

    public async ValueTask<List<CosmosItem<TDocument>>> PatchItemsIfMatch(List<CosmosItem<TDocument>> items,
        List<PatchOperation> operations, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CosmosItem<TDocument> item = items[i];
            ArgumentException.ThrowIfNullOrWhiteSpace(item.ETag);

            items[i] = await PatchItemIfMatchWithContainer(container, GetRequiredId(item.Document), operations, item.ETag, cancellationToken).NoSync();

            if (delay.HasValue)
                await DelayUtil.Delay(delay.Value, null, cancellationToken).NoSync();
        }

        return items;
    }

    private async ValueTask<List<TDocument>> PatchItemsCore(List<TDocument> documents, List<PatchOperation> operations, double? delayMs,
        bool useQueue, CancellationToken cancellationToken)
    {
        // Precompute delay once
        TimeSpan? timespanDelay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        if (timespanDelay.HasValue)
        {
            foreach (TDocument item in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await PatchItemCore(GetRequiredId(item), operations, useQueue, cancellationToken)
                    .NoSync();
                await DelayUtil.Delay(timespanDelay.Value, null, cancellationToken)
                               .NoSync();
            }
        }
        else
        {
            foreach (TDocument item in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await PatchItemCore(GetRequiredId(item), operations, useQueue, cancellationToken)
                    .NoSync();
            }
        }

        return documents;
    }

    public async ValueTask<TDocument?> PatchItem(string id, List<PatchOperation> operations, bool useQueue = false,
        CancellationToken cancellationToken = default)
    {
        return await PatchItemCore(id, operations, useQueue, cancellationToken).NoSync();
    }

    public async ValueTask<CosmosItem<TDocument>> PatchItemIfMatch(string id, List<PatchOperation> operations, string expectedETag,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedETag);
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        return await PatchItemIfMatchWithContainer(container, id, operations, expectedETag, cancellationToken).NoSync();
    }

    private async ValueTask<TDocument?> PatchItemCore(string id, List<PatchOperation> operations, bool useQueue,
        CancellationToken cancellationToken)
    {
        if (_log)
            Logger.LogDebug("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        (string partitionKey, string documentId) = id.ToSplitId();

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        if (useQueue)
        {
            // Snapshot ops so we don't retain caller's List/backing array.
            PatchOperation[] ops = operations.Count == 0 ? [] : operations.ToArray();

            bool auditEnabled = AuditEnabled;

            await _backgroundQueue.QueueValueTask(
                                      (Self: this, Container: container, PartitionKey: partitionKey, DocumentId: documentId, Ops: ops,
                                          AuditEnabled: auditEnabled, FullId: id), static async (s, token) =>
                                      {
                                          // This will throw on non-success (Cosmos SDK throws CosmosException)
                                          ItemResponse<TDocument> resp = await s
                                                                               .Container.PatchItemAsync<TDocument>(s.DocumentId,
                                                                                   new PartitionKey(s.PartitionKey), s.Ops, cancellationToken: token)
                                                                               .NoSync();

                                          // Audit only after success
                                          if (s.AuditEnabled)
                                          {
                                              await s.Self.CreateAuditItem(CrudEventType.Update, s.FullId, cancellationToken: token)
                                                     .NoSync();
                                          }
                                      }, cancellationToken)
                                  .NoSync();

            return null;
        }

        ItemResponse<TDocument> response = await container
                                                 .PatchItemAsync<TDocument>(documentId, new PartitionKey(partitionKey), operations, requestOptions: null,
                                                     cancellationToken: cancellationToken)
                                                 .NoSync();

        if (AuditEnabled)
            await CreateAuditItem(CrudEventType.Update, id, response.Resource, cancellationToken)
                .NoSync();

        return response.Resource;
    }

    private async ValueTask<CosmosItem<TDocument>> PatchItemIfMatchWithContainer(Microsoft.Azure.Cosmos.Container container, string id,
        IReadOnlyList<PatchOperation> operations, string expectedETag, CancellationToken cancellationToken)
    {
        if (_log)
            Logger.LogDebug("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        (string partitionKey, string documentId) = id.ToSplitId();
        var options = new PatchItemRequestOptions {IfMatchEtag = expectedETag};

        ItemResponse<TDocument> response = await container
                                                 .PatchItemAsync<TDocument>(documentId, new PartitionKey(partitionKey), operations, options,
                                                     cancellationToken)
                                                 .NoSync();

        if (AuditEnabled)
            await CreateAuditItem(CrudEventType.Update, id, response.Resource, cancellationToken).NoSync();

        return new CosmosItem<TDocument>(response.Resource, response.ETag);
    }
}

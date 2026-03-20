using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.ConcurrentProcessing.Executor;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Documents.Document;
using Soenneker.Dtos.IdPartitionPair;
using Soenneker.Enums.CrudEventTypes;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Delay;
using Soenneker.Utils.Method;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual ValueTask DeleteItem(string entityId, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = entityId.ToSplitId();

        return DeleteItem(documentId, partitionKey, useQueue, cancellationToken);
    }

    public virtual async ValueTask DeleteAll(double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name,
            delayMs.GetValueOrDefault());

        List<IdPartitionPair> ids = await GetAllIds(delayMs, cancellationToken)
            .NoSync();

        await DeleteIds(ids, delayMs, useQueue, cancellationToken)
            .NoSync();

        Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
    }

    public async ValueTask DeleteItems(IQueryable<TDocument> query, double? delayMs = null, bool useQueue = false,
        CancellationToken cancellationToken = default)
    {
        if (_log)
            Logger.LogWarning("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        List<IdPartitionPair> ids = await GetIds(query, delayMs, cancellationToken)
            .NoSync();

        await DeleteIds(ids, delayMs, useQueue, cancellationToken)
            .NoSync();
    }

    public async ValueTask DeleteItemsParallel(IQueryable<TDocument> query, int maxConcurrency, double? delayMs = null,
        CancellationToken cancellationToken = default)
    {
        if (_log)
            Logger.LogWarning("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        List<IdPartitionPair> ids = await GetIds(query, delayMs, cancellationToken)
            .NoSync();

        await DeleteIdsParallel(ids, maxConcurrency, cancellationToken)
            .NoSync();
    }

    public virtual async ValueTask DeleteIds(List<IdPartitionPair> ids, double? delayMs = null, bool useQueue = false,
        CancellationToken cancellationToken = default)
    {
        if (_log)
        {
            Logger.LogDebug("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name,
                delayMs.GetValueOrDefault());
        }

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        foreach (IdPartitionPair id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await DeleteItemWithContainer(container, id.Id, id.PartitionKey, useQueue, cancellationToken)
                .NoSync();

            if (delay.HasValue)
                await DelayUtil.Delay(delay.Value, null, cancellationToken)
                               .NoSync();
        }

        if (_log)
        {
            Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
        }
    }

    public virtual async ValueTask DeleteIdsParallel(List<IdPartitionPair> ids, int maxConcurrency, CancellationToken cancellationToken = default)
    {
        if (_log)
            Logger.LogDebug("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        var executor = new ConcurrentProcessingExecutor(maxConcurrency, Logger);

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        await executor.Execute(ids, async (id, ct) =>
                      {
                          ct.ThrowIfCancellationRequested();

                          await DeleteItemWithContainer(container, id.Id, id.PartitionKey, useQueue: false, ct: ct)
                              .NoSync();
                      }, cancellationToken)
                      .NoSync();

        if (_log)
            Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
    }

    public virtual async ValueTask DeleteItemWithContainer(Microsoft.Azure.Cosmos.Container container, string documentId, string partitionKey,
        bool useQueue = false, CancellationToken ct = default)
    {
        if (_log)
        {
            Logger.LogDebug("-- COSMOS: {method} ({type}): DocID: {documentId}, PartitionKey: {partitionKey}", MethodUtil.Get(), typeof(TDocument).Name,
                documentId, partitionKey);
        }

        var pk = new PartitionKey(partitionKey);
        ItemRequestOptions options = CosmosRequestOptions.ExcludeResponse;

        // Only compute entityId if we will audit
        bool auditEnabled = AuditEnabled;
        string entityId = auditEnabled ? documentId.AddPartitionKey(partitionKey) : string.Empty;

        if (useQueue)
        {
            await _backgroundQueue.QueueValueTask(
                                      (Self: this, Container: container, DocumentId: documentId, Pk: pk, Options: options, AuditEnabled: auditEnabled,
                                          EntityId: entityId), static async (s, token) =>
                                      {
                                          using ResponseMessage resp = await s.Container.DeleteItemStreamAsync(s.DocumentId, s.Pk, s.Options, token)
                                                                              .NoSync();

                                          // Decide your semantics:
                                          // - If deleting a missing doc is "fine", ignore 404.
                                          // - Otherwise, call EnsureSuccessStatusCode().
                                          if (resp.StatusCode != System.Net.HttpStatusCode.NotFound)
                                              resp.EnsureSuccessStatusCode();

                                          // Only write audit after the delete is known-good (or NotFound accepted)
                                          if (s.AuditEnabled)
                                          {
                                              await s.Self.CreateAuditItem(CrudEventType.Delete, s.EntityId, cancellationToken: token)
                                                     .NoSync();
                                          }
                                      }, ct)
                                  .NoSync();

            return;
        }

        using ResponseMessage resp2 = await container.DeleteItemStreamAsync(documentId, pk, options, ct)
                                                     .NoSync();

        if (resp2.StatusCode != System.Net.HttpStatusCode.NotFound)
            resp2.EnsureSuccessStatusCode();

        if (auditEnabled)
            await CreateAuditItem(CrudEventType.Delete, entityId, cancellationToken: ct)
                .NoSync();
    }

    public virtual async ValueTask DeleteItem(string documentId, string partitionKey, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        await DeleteItemWithContainer(container, documentId, partitionKey, useQueue, cancellationToken).NoSync();
    }

    public virtual async ValueTask DeleteCreatedAtBetween(DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        QueryDefinition q = new QueryDefinition("SELECT VALUE { id: c.id, pk: c.partitionKey } FROM c WHERE c.createdAt >= @s AND c.createdAt <= @e")
                            .WithParameter("@s", startAt)
                            .WithParameter("@e", endAt);

        using FeedIterator<IdPartitionPair> it = container.GetItemQueryIterator<IdPartitionPair>(q);

        var ids = new List<IdPartitionPair>(256);

        while (it.HasMoreResults)
        {
            FeedResponse<IdPartitionPair> page = await it.ReadNextAsync(cancellationToken)
                                                         .NoSync();
            ids.EnsureCapacity(ids.Count + page.Count);

            foreach (IdPartitionPair p in page)
            {
                ids.Add(p);
            }
        }

        await DeleteIdsBatched(ids, 100, cancellationToken)
            .NoSync();
    }

    public virtual async ValueTask DeleteIdsBatched(List<IdPartitionPair> ids, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return;

        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        var buckets = new Dictionary<string, List<string>>(Math.Min(ids.Count, 256), StringComparer.Ordinal);

        for (int i = 0; i < ids.Count; i++)
        {
            IdPartitionPair pair = ids[i];

            if (!buckets.TryGetValue(pair.PartitionKey, out List<string>? bucket))
            {
                bucket = new List<string>(Math.Min(batchSize, 16));
                buckets[pair.PartitionKey] = bucket;
            }

            bucket.Add(pair.Id);
        }

        foreach (KeyValuePair<string, List<string>> kvp in buckets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PartitionKey pk = new(kvp.Key);
            List<string> bucket = kvp.Value;

            for (int start = 0; start < bucket.Count; start += batchSize)
            {
                int count = Math.Min(batchSize, bucket.Count - start);

                await ExecuteDeleteBatch(container, pk, bucket, start, count, cancellationToken)
                    .NoSync();
            }
        }
    }

    private static async ValueTask ExecuteDeleteBatch(Microsoft.Azure.Cosmos.Container container, PartitionKey partitionKey, List<string> ids, int start,
        int count, CancellationToken cancellationToken)
    {
        TransactionalBatch batch = container.CreateTransactionalBatch(partitionKey);

        int end = start + count;
        for (int i = start; i < end; i++)
        {
            batch = batch.DeleteItem(ids[i]);
        }

        using TransactionalBatchResponse resp = await batch.ExecuteAsync(cancellationToken)
                                                           .NoSync();
    }
}
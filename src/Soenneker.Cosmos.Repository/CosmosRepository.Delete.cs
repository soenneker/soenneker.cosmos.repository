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
    public virtual async ValueTask DeleteItem(string entityId, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = entityId.ToSplitId();

        await DeleteItem(documentId, partitionKey, useQueue, cancellationToken)
            .NoSync();
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

        TimeSpan? timeSpanDelay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        if (timeSpanDelay.HasValue)
        {
            foreach (IdPartitionPair id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await DeleteItemWithContainer(container, id.Id, id.PartitionKey, useQueue, cancellationToken)
                    .NoSync();

                await DelayUtil.Delay(timeSpanDelay.Value, null, cancellationToken)
                               .NoSync();
            }
        }
        else
        {
            foreach (IdPartitionPair id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await DeleteItemWithContainer(container, id.Id, id.PartitionKey, useQueue, cancellationToken)
                    .NoSync();
            }
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

        var states = new List<DeleteState>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            IdPartitionPair id = ids[i];
            states.Add(new DeleteState(Self: this, Container: container, DocumentId: id.Id, PartitionKey: id.PartitionKey));
        }

        await executor.Execute(states, static async (s, ct) =>
                      {
                          ct.ThrowIfCancellationRequested();

                          await s.Self.DeleteItemWithContainer(s.Container, s.DocumentId, s.PartitionKey, useQueue: false, ct: ct)
                                 .NoSync();
                      }, cancellationToken)
                      .NoSync();

        if (_log)
            Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
    }

    private readonly record struct DeleteState(
        CosmosRepository<TDocument> Self,
        Microsoft.Azure.Cosmos.Container Container,
        string DocumentId,
        string PartitionKey);

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
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        await DeleteItemWithContainer(container, documentId, partitionKey, useQueue, cancellationToken)
            .NoSync();
    }

    public virtual async ValueTask DeleteCreatedAtBetween(DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        QueryDefinition? q = new QueryDefinition("SELECT VALUE { id: c.id, pk: c.partitionKey } " + "FROM c WHERE c.createdAt >= @s AND c.createdAt <= @e")
                             .WithParameter("@s", startAt)
                             .WithParameter("@e", endAt);

        using FeedIterator<IdPartitionPair>? it = container.GetItemQueryIterator<IdPartitionPair>(q);

        var ids = new List<IdPartitionPair>(256);

        while (it.HasMoreResults)
        {
            FeedResponse<IdPartitionPair>? page = await it.ReadNextAsync(cancellationToken)
                                                          .NoSync();
            ids.EnsureCapacity(ids.Count + page.Count);

            foreach (IdPartitionPair? p in page)
            {
                ids.Add(p);
            }
        }

        // Prefer batched deletes by PK when ranges are large:
        await DeleteIdsBatched(ids, 100, cancellationToken)
            .NoSync();
    }

    public virtual async ValueTask DeleteIdsBatched(List<IdPartitionPair> ids, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        // Group by PK to use TransactionalBatch
        foreach (IGrouping<string, IdPartitionPair> group in ids.GroupBy(x => x.PartitionKey))
        {
            var pk = new PartitionKey(group.Key);
            var buffer = new List<string>(batchSize);

            foreach (IdPartitionPair item in group)
            {
                buffer.Add(item.Id);

                if (buffer.Count == batchSize)
                {
                    await ExecuteDeleteBatch(container, pk, buffer, cancellationToken)
                        .NoSync();
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
                await ExecuteDeleteBatch(container, pk, buffer, cancellationToken)
                    .NoSync();
        }
    }

    private static async ValueTask ExecuteDeleteBatch(Microsoft.Azure.Cosmos.Container container, PartitionKey partitionKey, List<string> ids,
        CancellationToken cancellationToken)
    {
        TransactionalBatch batch = container.CreateTransactionalBatch(partitionKey);

        foreach (string id in ids)
        {
            batch = batch.DeleteItem(id);
        }

        using TransactionalBatchResponse resp = await batch.ExecuteAsync(cancellationToken)
                                                           .NoSync();
        // Optional: validate resp.IsSuccessStatusCode or inspect per-op results
    }
}
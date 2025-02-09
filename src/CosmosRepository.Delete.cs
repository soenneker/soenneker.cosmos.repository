using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.ConcurrentProcessing.Executor;
using Soenneker.Cosmos.RequestOptions;
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
    public virtual async ValueTask DeleteItem(string entityId, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = entityId.ToSplitId();

        await DeleteItem(documentId, partitionKey, useQueue, cancellationToken)
            .NoSync();
    }

    public virtual async ValueTask DeleteAll(double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name, delayMs.GetValueOrDefault());

        List<IdPartitionPair> ids = await GetAllIds(delayMs, cancellationToken)
            .NoSync();

        await DeleteIds(ids, delayMs, useQueue, cancellationToken)
            .NoSync();

        Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
    }

    public async ValueTask DeleteItems(IQueryable<TDocument> query, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        if (_log)
            Logger.LogWarning("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        List<IdPartitionPair> ids = await GetIds(query, delayMs, cancellationToken)
            .NoSync();

        await DeleteIds(ids, delayMs, useQueue, cancellationToken)
            .NoSync();
    }

    public async ValueTask DeleteItemsParallel(IQueryable<TDocument> query, int maxConcurrency, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        if (_log)
            Logger.LogWarning("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        List<IdPartitionPair> ids = await GetIds(query, delayMs, cancellationToken)
            .NoSync();

        await DeleteIdsParallel(ids, maxConcurrency, cancellationToken)
            .NoSync();
    }

    public virtual async ValueTask DeleteIds(List<IdPartitionPair> ids, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        if (_log)
        {
            Logger.LogDebug("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name, delayMs.GetValueOrDefault());
        }

        TimeSpan? timeSpanDelay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        if (timeSpanDelay.HasValue)
        {
            foreach (IdPartitionPair id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await DeleteItem(id.Id, id.PartitionKey, useQueue, cancellationToken)
                    .NoSync();
                await Task.Delay(timeSpanDelay.Value, cancellationToken)
                          .NoSync();
            }
        }
        else
        {
            foreach (IdPartitionPair id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await DeleteItem(id.Id, id.PartitionKey, useQueue, cancellationToken)
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
        {
            Logger.LogDebug("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
        }

        var executor = new ConcurrentProcessingExecutor(maxConcurrency, Logger);

        var list = new List<Func<Task>>();

        foreach (IdPartitionPair id in ids)
        {
            list.Add(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await DeleteItem(id.Id, id.PartitionKey, false, cancellationToken)
                    .NoSync();
            });
        }

        await executor.Execute(list, cancellationToken).NoSync();

        if (_log)
        {
            Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
        }
    }

    public virtual async ValueTask DeleteItem(string documentId, string partitionKey, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        if (_log)
            Logger.LogDebug("-- COSMOS: {method} ({type}): DocID: {documentId}, PartitionKey: {partitionKey}", MethodUtil.Get(), typeof(TDocument).Name, documentId, partitionKey);

        var partitionKeyObj = new PartitionKey(partitionKey);

        if (useQueue)
        {
            await _backgroundQueue.QueueValueTask(async token =>
                                  {
                                      Microsoft.Azure.Cosmos.Container container = await Container(token)
                                          .NoSync();

                                      _ = await container.DeleteItemStreamAsync(documentId, partitionKeyObj, CosmosRequestOptions.ExcludeResponse, token)
                                                         .NoSync();
                                  }, cancellationToken)
                                  .NoSync();
        }
        else
        {
            Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
                .NoSync();

            _ = await container.DeleteItemStreamAsync(documentId, partitionKeyObj, CosmosRequestOptions.ExcludeResponse, cancellationToken)
                               .NoSync();
        }

        string entityId = documentId.AddPartitionKey(partitionKey);

        if (AuditEnabled)
            await CreateAuditItem(EventType.Delete, entityId, cancellationToken: cancellationToken)
                .NoSync();
    }

    public virtual async ValueTask DeleteCreatedAtBetween(DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable<TDocument>(null, cancellationToken)
            .NoSync();
        query = query.Where(b => b.CreatedAt >= startAt && b.CreatedAt <= endAt);

        await DeleteItems(query, cancellationToken: cancellationToken)
            .NoSync();
    }
}
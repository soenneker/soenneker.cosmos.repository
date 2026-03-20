using Microsoft.Azure.Cosmos;
using Soenneker.Documents.Document;
using Soenneker.Dtos.IdNamePair;
using Soenneker.Dtos.IdPartitionPair;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Delay;
using Soenneker.Utils.Method;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Utils.PooledStringBuilders;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask<List<TDocument>> GetAll(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();
        var q = new QueryDefinition("SELECT * FROM c");

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        using FeedIterator<TDocument> it = container.GetItemQueryIterator<TDocument>(q);
        return await DrainIterator(it, delay, cancellationToken)
            .NoSync();
    }

    public async ValueTask<List<TDocument>> GetAllByPartitionKey(string partitionKey, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();
        var q = new QueryDefinition("SELECT * FROM c");

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        using FeedIterator<TDocument> it = container.GetItemQueryIterator<TDocument>(q, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey)
        });

        return await DrainIterator(it, delay, cancellationToken)
            .NoSync();
    }

    public async ValueTask<List<TDocument>> GetAllByDocumentIds(List<string> documentIds, CancellationToken cancellationToken = default)
    {
        int count = documentIds.Count;
        if (count == 0)
            return [];

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();
        var results = new List<TDocument>(count);

        for (int offset = 0; offset < count; offset += _documentIdBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int batchCount = Math.Min(_documentIdBatchSize, count - offset);

            var queryDefinition = BuildIdInQuery(documentIds, offset, batchCount);

            using FeedIterator<TDocument> iterator = container.GetItemQueryIterator<TDocument>(queryDefinition);

            while (iterator.HasMoreResults)
            {
                FeedResponse<TDocument> page = await iterator.ReadNextAsync(cancellationToken)
                                                             .NoSync();

                if (page.Count > 0)
                {
                    results.EnsureCapacity(results.Count + page.Count);

                    foreach (TDocument item in page)
                    {
                        results.Add(item);
                    }
                }
            }
        }

        return results;
    }

    public async ValueTask<List<TDocument>> GetAllByIdPartitionPairs(List<IdPartitionPair> pairs, CancellationToken cancellationToken = default)
    {
        int count = pairs.Count;
        if (count == 0)
            return [];

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        var items = new List<(string id, PartitionKey pk)>(count);

        for (int i = 0; i < count; i++)
        {
            IdPartitionPair pair = pairs[i];
            items.Add((pair.Id, new PartitionKey(pair.PartitionKey)));
        }

        FeedResponse<TDocument> response = await container.ReadManyItemsAsync<TDocument>(items, cancellationToken: cancellationToken)
                                                          .NoSync();

        return response.Resource as List<TDocument> ?? response.Resource.ToList();
    }

    public ValueTask<List<TDocument>> GetAllByIdNamePairs(List<IdNamePair> pairs, CancellationToken cancellationToken = default)
    {
        int count = pairs.Count;

        if (count == 0)
            return new ValueTask<List<TDocument>>([]);

        var idPartitionPairs = new List<IdPartitionPair>(count);

        for (int i = 0; i < count; i++)
        {
            IdNamePair pair = pairs[i];

            idPartitionPairs.Add(new IdPartitionPair
            {
                Id = pair.Id,
                PartitionKey = pair.Id
            });
        }

        return GetAllByIdPartitionPairs(idPartitionPairs, cancellationToken);
    }

    public ValueTask<List<TDocument>> GetItems(string query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        return GetItems<TDocument>(query, delayMs, cancellationToken);
    }

    public ValueTask<List<T>> GetItems<T>(string query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        return GetItems<T>(new QueryDefinition(query), delayMs, cancellationToken);
    }

    public ValueTask<List<TDocument>> GetItems(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        return GetItems<TDocument>(queryDefinition, delayMs, cancellationToken);
    }

    public virtual async ValueTask<List<IdPartitionPair>> GetAllIds(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        var qd = new QueryDefinition("SELECT VALUE { id: c.id, partitionKey: c.partitionKey } FROM c");
        return await GetIds(qd, null, delayMs, cancellationToken)
            .NoSync();
    }

    public async ValueTask<List<IdPartitionPair>> GetIds(QueryDefinition queryDefinition, QueryRequestOptions? options = null, double? delayMs = null,
        CancellationToken cancellationToken = default)
    {
        LogQuery<IdPartitionPair>(queryDefinition, MethodUtil.Get());

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();
        using FeedIterator<IdPartitionPair> it = container.GetItemQueryIterator<IdPartitionPair>(queryDefinition, requestOptions: options);

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        var results = new List<IdPartitionPair>(128);

        while (it.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FeedResponse<IdPartitionPair> page = await it.ReadNextAsync(cancellationToken)
                                                         .NoSync();
            int pageCount = page.Count;

            if (pageCount > 0)
            {
                results.EnsureCapacity(results.Count + pageCount);

                foreach (IdPartitionPair item in page)
                {
                    results.Add(item);
                }

                if (delay.HasValue)
                    await DelayUtil.Delay(delay.Value, null, cancellationToken)
                                   .NoSync();
            }
        }

        return results;
    }

    public ValueTask<List<IdPartitionPair>> GetIds(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<IdPartitionPair> idQueryable = query.Select(static d => new IdPartitionPair
        {
            Id = d.DocumentId,
            PartitionKey = d.PartitionKey
        });

        return GetItems(idQueryable, delayMs, cancellationToken);
    }

    public async ValueTask<List<string>> GetAllPartitionKeys(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();
        var q = new QueryDefinition("SELECT DISTINCT VALUE c.partitionKey FROM c");

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        using FeedIterator<string> it = container.GetItemQueryIterator<string>(q);

        return await DrainIterator(it, delay, cancellationToken)
            .NoSync();
    }

    public ValueTask<List<string>> GetPartitionKeys(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<string> idQueryable = query.Select(static d => d.PartitionKey)
                                              .Distinct();

        return GetItems(idQueryable, delayMs, cancellationToken);
    }

    public async ValueTask<List<T>> GetItems<T>(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        LogQuery<T>(queryDefinition, MethodUtil.Get());

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        using FeedIterator<T> it = container.GetItemQueryIterator<T>(queryDefinition);
        return await DrainIterator(it, delay, cancellationToken)
            .NoSync();
    }

    private static async ValueTask<List<T>> DrainIterator<T>(FeedIterator<T> iterator, TimeSpan? interPageDelay, CancellationToken cancellationToken)
    {
        var results = new List<T>(16);

        while (iterator.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FeedResponse<T> page = await iterator.ReadNextAsync(cancellationToken)
                                                 .NoSync();
            int pageCount = page.Count;

            if (pageCount > 0)
            {
                results.EnsureCapacity(results.Count + pageCount);

                foreach (T item in page)
                {
                    results.Add(item);
                }

                if (interPageDelay.HasValue)
                    await DelayUtil.Delay(interPageDelay.Value, null, cancellationToken)
                                   .NoSync();
            }
        }

        return results;
    }

    public virtual async ValueTask<List<TDocument>> GetItemsBetween(DateTimeOffset startAt, DateTimeOffset endAt, double? delayMs = null,
        CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        QueryDefinition q = new QueryDefinition("SELECT * FROM c WHERE c.createdAt >= @s AND c.createdAt <= @e").WithParameter("@s", startAt)
            .WithParameter("@e", endAt);

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        using FeedIterator<TDocument> it = container.GetItemQueryIterator<TDocument>(q);
        return await DrainIterator(it, delay, cancellationToken)
            .NoSync();
    }

    private static QueryDefinition BuildIdInQuery(List<string> documentIds, int offset, int count)
    {
        int estimatedLength = 32 + count * 6;
        using var query = new PooledStringBuilder(estimatedLength);

        query.Append("SELECT * FROM c WHERE c.id IN (");

        for (int i = 0; i < count; i++)
        {
            if (i != 0)
                query.Append(',');

            query.Append(CosmosRepositoryStatics.IdParameterNames[i]);
        }

        query.Append(')');

        QueryDefinition queryDefinition = new(query.ToString());

        for (int i = 0; i < count; i++)
        {
            queryDefinition.WithParameter(CosmosRepositoryStatics.IdParameterNames[i], documentIds[offset + i]);
        }

        return queryDefinition;
    }
}
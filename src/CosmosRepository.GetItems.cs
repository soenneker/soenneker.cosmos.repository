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

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask<List<TDocument>> GetAll(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        var q = new QueryDefinition("SELECT * FROM c"); // full scan (use sparingly)

        using FeedIterator<TDocument>? it = container.GetItemQueryIterator<TDocument>(q);
        return await DrainIterator(it, delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null, cancellationToken).NoSync();
    }

    public async ValueTask<List<TDocument>> GetAllByPartitionKey(string partitionKey, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        var q = new QueryDefinition("SELECT * FROM c");

        using FeedIterator<TDocument>? it = container.GetItemQueryIterator<TDocument>(q, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey)
        });

        return await DrainIterator(it, delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null, cancellationToken).NoSync();
    }

    public async ValueTask<List<TDocument>> GetAllByDocumentIds(List<string> documentIds, CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0)
            return [];

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        const int batchSize = 50;
        var all = new List<TDocument>(documentIds.Count);

        for (var i = 0; i < documentIds.Count; i += batchSize)
        {
            string[] slice = documentIds.Skip(i).Take(batchSize).ToArray();
            string inParams = string.Join(",", Enumerable.Range(0, slice.Length).Select(j => $"@i{j}"));
            var qd = new QueryDefinition($"SELECT * FROM c WHERE c.id IN ({inParams})");

            for (var j = 0; j < slice.Length; j++)
            {
                qd.WithParameter($"@i{j}", slice[j]);
            }

            using FeedIterator<TDocument>? it = container.GetItemQueryIterator<TDocument>(qd);

            while (it.HasMoreResults)
            {
                FeedResponse<TDocument>? page = await it.ReadNextAsync(cancellationToken).NoSync();
                all.AddRange(page);
            }
        }

        return all;
    }

    public async ValueTask<List<TDocument>> GetAllByIdPartitionPairs(List<IdPartitionPair> pairs, CancellationToken cancellationToken = default)
    {
        if (pairs.Count == 0)
            return [];

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        var items = new List<(string id, PartitionKey pk)>(pairs.Count);

        foreach (IdPartitionPair p in pairs)
        {
            items.Add((p.Id, new PartitionKey(p.PartitionKey)));
        }

        FeedResponse<TDocument>? resp = await container.ReadManyItemsAsync<TDocument>(items, cancellationToken: cancellationToken).NoSync();
        return resp.Resource.ToList();
    }

    public ValueTask<List<TDocument>> GetAllByIdNamePairs(List<IdNamePair> pairs, CancellationToken cancellationToken = default)
    {
        if (pairs.Count == 0)
            return new ValueTask<List<TDocument>>([]);

        List<IdPartitionPair> idPartitionPairs = [];

        foreach (IdNamePair pair in pairs)
        {
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
        return await GetIds(qd, null, delayMs, cancellationToken).NoSync();
    }

    public async ValueTask<List<IdPartitionPair>> GetIds(QueryDefinition queryDefinition, QueryRequestOptions? options = null, double? delayMs = null,
        CancellationToken cancellationToken = default)
    {
        LogQuery<IdPartitionPair>(queryDefinition, MethodUtil.Get());

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        using FeedIterator<IdPartitionPair>? it = container.GetItemQueryIterator<IdPartitionPair>(queryDefinition, requestOptions: options);

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        var results = new List<IdPartitionPair>(128);

        while (it.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FeedResponse<IdPartitionPair>? page = await it.ReadNextAsync(cancellationToken).NoSync();

            if (page.Count > 0)
            {
                results.EnsureCapacity(results.Count + page.Count);

                foreach (IdPartitionPair? item in page)
                {
                    results.Add(item);
                }

                if (delay.HasValue)
                    await DelayUtil.Delay(delay.Value, null, cancellationToken).NoSync();
            }
        }

        return results;
    }

    public ValueTask<List<IdPartitionPair>> GetIds(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<IdPartitionPair> idQueryable = query.Select(d => new IdPartitionPair {Id = d.DocumentId, PartitionKey = d.PartitionKey});

        return GetItems(idQueryable, delayMs, cancellationToken);
    }

    public async ValueTask<List<string>> GetAllPartitionKeys(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        var q = new QueryDefinition("SELECT DISTINCT VALUE c.partitionKey FROM c");

        using FeedIterator<string>? it = container.GetItemQueryIterator<string>(q);

        return await DrainIterator(it, delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null, cancellationToken).NoSync();
    }

    public ValueTask<List<string>> GetPartitionKeys(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<string> idQueryable = query.Select(d => d.PartitionKey).Distinct();

        return GetItems(idQueryable, delayMs, cancellationToken);
    }

    public async ValueTask<List<T>> GetItems<T>(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        LogQuery<T>(queryDefinition, MethodUtil.Get());

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        using FeedIterator<T>? it = container.GetItemQueryIterator<T>(queryDefinition);
        return await DrainIterator(it, delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null, cancellationToken).NoSync();
    }

    private static async ValueTask<List<T>> DrainIterator<T>(FeedIterator<T> iterator, TimeSpan? interPageDelay, CancellationToken cancellationToken)
    {
        // Start small; we’ll grow with EnsureCapacity
        var results = new List<T>(16);

        while (iterator.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FeedResponse<T> page = await iterator.ReadNextAsync(cancellationToken).NoSync();

            // Grow only as needed (avoids repeated reallocations)
            if (page.Count > 0)
                results.EnsureCapacity(results.Count + page.Count);

            // Manual copy cheaper than AddRange’s IEnumerable path
            foreach (T item in page)
            {
                results.Add(item);
            }

            if (interPageDelay.HasValue && page.Count > 0)
                await DelayUtil.Delay(interPageDelay.Value, null, cancellationToken).NoSync();
        }

        return results;
    }

    public virtual async ValueTask<List<TDocument>> GetItemsBetween(DateTime startAt, DateTime endAt, double? delayMs = null,
        CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        QueryDefinition? q = new QueryDefinition("SELECT * FROM c WHERE c.createdAt >= @s AND c.createdAt <= @e").WithParameter("@s", startAt)
            .WithParameter("@e", endAt);

        using FeedIterator<TDocument>? it = container.GetItemQueryIterator<TDocument>(q);
        return await DrainIterator(it, delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null, cancellationToken).NoSync();
    }
}
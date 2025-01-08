using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Documents.Document;
using Soenneker.Dtos.IdNamePair;
using Soenneker.Dtos.IdPartitionPair;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask<List<TDocument>> GetAll(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.Select(d => d);

        return await GetItems(query, delayMs, cancellationToken).NoSync();
    }

    public async ValueTask<List<TDocument>> GetAllByPartitionKey(string partitionKey, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.Where(c => c.PartitionKey == partitionKey);

        return await GetItems(query, delayMs, cancellationToken).NoSync();
    }

    public async ValueTask<List<TDocument>> GetAllByDocumentIds(IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.Where(c => documentIds.Contains(c.DocumentId));

        return await GetItems(query, cancellationToken: cancellationToken).NoSync();
    }

    public async ValueTask<List<TDocument>?> GetAllByIdNamePairs(IEnumerable<IdNamePair> pairs, CancellationToken cancellationToken = default)
    {
        IEnumerable<string> ids = pairs.Select(c => c.Id);

        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.Where(c => ids.Contains(c.Id));

        return await GetItems(query, cancellationToken: cancellationToken).NoSync();
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
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();

        return await GetIds(query, delayMs, cancellationToken).NoSync();
    }

    public ValueTask<List<IdPartitionPair>> GetIds(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<IdPartitionPair> idQueryable = query.Select(d => new IdPartitionPair {Id = d.DocumentId, PartitionKey = d.PartitionKey});

        return GetItems(idQueryable, delayMs, cancellationToken);
    }

    public async ValueTask<List<string>> GetAllPartitionKeys(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();

        return await GetPartitionKeys(query, delayMs, cancellationToken).NoSync();
    }

    public ValueTask<List<string>> GetPartitionKeys(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<string> idQueryable = query.Select(d => d.PartitionKey);

        return GetItems(idQueryable, delayMs, cancellationToken);
    }

    public async ValueTask<List<T>> GetItems<T>(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        LogQuery<T>(queryDefinition, MethodUtil.Get());

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        using FeedIterator<T> iterator = container.GetItemQueryIterator<T>(queryDefinition);

        var results = new List<T>();

        if (delayMs.HasValue)
        {
            TimeSpan timeSpanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

            while (iterator.HasMoreResults)
            {
                FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken).NoSync();
                results.AddRange(response);

                await Task.Delay(timeSpanDelay, cancellationToken).NoSync();
            }
        }
        else
        {
            while (iterator.HasMoreResults)
            {
                FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken).NoSync();
                results.AddRange(response);
            }
        }

        return results;
    }

    public virtual async ValueTask<List<TDocument>> GetItemsBetween(DateTime startAt, DateTime endAt, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.Where(c => c.CreatedAt >= startAt && c.CreatedAt <= endAt);

        return await GetItems(query, delayMs, cancellationToken).NoSync();
    }
}
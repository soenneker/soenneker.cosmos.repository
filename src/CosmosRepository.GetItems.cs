using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Dtos.IdPartitionPair;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask<List<TDocument>> GetAll(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> queryable = await BuildQueryable(cancellationToken).NoSync();
        queryable = queryable.Select(d => d);

        List<TDocument> results = await GetItems(queryable, delayMs, cancellationToken).NoSync();
        return results;
    }

    public async ValueTask<List<TDocument>> GetAllByPartitionKey(string partitionKey, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> queryable = await BuildQueryable(cancellationToken).NoSync();
        queryable = queryable.Where(c => c.PartitionKey == partitionKey);

        List<TDocument> results = await GetItems(queryable, delayMs, cancellationToken).NoSync();
        return results;
    }

    public async ValueTask<List<TDocument>> GetAllByDocumentIds(IEnumerable<string> documentIds, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.Where(b => documentIds.Contains(b.DocumentId));
        List<TDocument> docs = await GetItems(query, cancellationToken: cancellationToken).NoSync();
        return docs;
    }

    public ValueTask<List<TDocument>> GetItems(string query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        return GetItems<TDocument>(query, delayMs, cancellationToken);
    }

    public ValueTask<List<T>> GetItems<T>(string query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        return GetItems<T>(new QueryDefinition(query), delayMs, cancellationToken);
    }

    public ValueTask<List<TDocument>> GetItems<TResponse>(ODataQueryOptions odataOptions, CancellationToken cancellationToken = default)
    {
        return GetItems<TDocument, TResponse>(odataOptions, cancellationToken);
    }

    public ValueTask<List<TDocument>> GetItems(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        return GetItems<TDocument>(queryDefinition, delayMs, cancellationToken);
    }

    public async ValueTask<List<T>> GetItems<T, TResponse>(ODataQueryOptions odataOptions, CancellationToken cancellationToken = default)
    {
        IQueryable<TResponse> queryable = await BuildQueryable<TResponse>(cancellationToken).NoSync();

        List<T> result = await GetItems<T, TResponse>(odataOptions, queryable, cancellationToken).NoSync();

        return result;
    }

    public virtual async ValueTask<List<IdPartitionPair>> GetAllIds(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> queryable = await BuildQueryable(cancellationToken).NoSync();

        List<IdPartitionPair> results = await GetIds(queryable, delayMs, cancellationToken).NoSync();

        return results;
    }

    public ValueTask<List<IdPartitionPair>> GetIds(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<IdPartitionPair> idQueryable = query.Select(d => new IdPartitionPair {Id = d.DocumentId, PartitionKey = d.PartitionKey});

        return GetItems(idQueryable, delayMs, cancellationToken);
    }

    public async ValueTask<List<string>> GetAllPartitionKeys(double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> queryable = await BuildQueryable(cancellationToken).NoSync();

        List<string> results = await GetPartitionKeys(queryable, delayMs, cancellationToken).NoSync();

        return results;
    }

    public ValueTask<List<string>> GetPartitionKeys(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<string> idQueryable = query.Select(d => d.PartitionKey);

        return GetItems(idQueryable, delayMs, cancellationToken);
    }

    public ValueTask<List<T>> GetItems<T, TResponse>(ODataQueryOptions odataOptions, IQueryable query, CancellationToken cancellationToken = default)
    {
        var odataQuery = (IQueryable<TResponse>) odataOptions.ApplyTo(query);

        var definition = odataQuery.ToQueryDefinition();

        if (definition == null)
            Logger.LogError("ODataDefinition was null after the application! {type}", typeof(TResponse).Name);

        string queryText = definition != null ? definition.QueryText : "SELECT * FROM c";

        ValueTask<List<T>> results = GetItems<T>(queryText, cancellationToken: cancellationToken);

        return results;
    }

    public async ValueTask<List<T>> GetItems<T>(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        LogQuery<T>(queryDefinition, MethodUtil.Get());

        TimeSpan? timeSpanDelay = null;

        if (delayMs != null)
            timeSpanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        using FeedIterator<T> iterator = container.GetItemQueryIterator<T>(queryDefinition);

        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken).NoSync();

            results.AddRange(response.ToList()); // TODO: I wonder if this is faster than foreach (response.Resource)

            if (delayMs != null)
                await Task.Delay(timeSpanDelay!.Value, cancellationToken: cancellationToken).NoSync();
        }

        return results;
    }

    public virtual async ValueTask<List<TDocument>> GetItemsBetween(DateTime startAt, DateTime endAt, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.Where(c => c.CreatedAt >= startAt && c.CreatedAt <= endAt);

        List<TDocument> items = await GetItems(query, delayMs, cancellationToken).NoSync();

        return items;
    }
}
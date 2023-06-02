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
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask<List<TDocument>> GetAll(double? delayMs = null)
    {
        IQueryable<TDocument> queryable = await BuildQueryable();
        queryable = queryable.Select(d => d);

        List<TDocument> results = await GetItems(queryable, delayMs);
        return results;
    }

    public async ValueTask<List<TDocument>> GetAllByDocumentIds(IEnumerable<string> documentIds)
    {
        IQueryable<TDocument> query = await BuildQueryable();
        query = query.Where(b => documentIds.Contains(b.DocumentId));
        List<TDocument> docs = await GetItems(query);
        return docs;
    }

    public ValueTask<List<TDocument>> GetItems(string query, double? delayMs = null)
    {
        return GetItems<TDocument>(query, delayMs);
    }

    public ValueTask<List<T>> GetItems<T>(string query, double? delayMs = null)
    {
        return GetItems<T>(new QueryDefinition(query), delayMs);
    }

    public ValueTask<List<TDocument>> GetItems<TResponse>(ODataQueryOptions odataOptions)
    {
        return GetItems<TDocument, TResponse>(odataOptions);
    }

    public ValueTask<List<TDocument>> GetItems(QueryDefinition queryDefinition, double? delayMs = null)
    {
        return GetItems<TDocument>(queryDefinition, delayMs);
    }

    public async ValueTask<List<T>> GetItems<T, TResponse>(ODataQueryOptions odataOptions)
    {
        IQueryable<TResponse> queryable = await BuildQueryable<TResponse>();

        List<T> result = await GetItems<T, TResponse>(odataOptions, queryable);

        return result;
    }

    public virtual async ValueTask<List<IdPartitionPair>> GetAllIds(double? delayMs = null)
    {
        IQueryable<TDocument> queryable = await BuildQueryable();

        List<IdPartitionPair> results = await GetIds(queryable, delayMs);

        return results;
    }

    public ValueTask<List<IdPartitionPair>> GetIds(IQueryable<TDocument> query, double? delayMs = null)
    {
        IQueryable<IdPartitionPair> idQueryable = query.Select(d => new IdPartitionPair {Id = d.DocumentId, PartitionKey = d.PartitionKey});

        return GetItems(idQueryable, delayMs);
    }

    public async ValueTask<List<string>> GetAllPartitionKeys(double? delayMs = null)
    {
        IQueryable<TDocument> queryable = await BuildQueryable();

        List<string> results = await GetPartitionKeys(queryable, delayMs);

        return results;
    }

    public ValueTask<List<string>> GetPartitionKeys(IQueryable<TDocument> query, double? delayMs = null)
    {
        IQueryable<string> idQueryable = query.Select(d => d.PartitionKey);

        return GetItems(idQueryable, delayMs);
    }

    public ValueTask<List<T>> GetItems<T, TResponse>(ODataQueryOptions odataOptions, IQueryable query)
    {
        var odataQuery = (IQueryable<TResponse>) odataOptions.ApplyTo(query);

        var definition = odataQuery.ToQueryDefinition();

        if (definition == null)
            Logger.LogError("ODataDefinition was null after the application! {type}", typeof(TResponse).Name);

        string queryText = definition != null ? definition.QueryText : "SELECT * FROM c";

        ValueTask<List<T>> results = GetItems<T>(queryText);

        return results;
    }

    public async ValueTask<List<T>> GetItems<T>(QueryDefinition queryDefinition, double? delayMs = null)
    {
        LogQuery<T>(queryDefinition, MethodUtil.Get());

        TimeSpan? timeSpanDelay = null;

        if (delayMs != null)
            timeSpanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

        Microsoft.Azure.Cosmos.Container container = await Container;

        using FeedIterator<T> iterator = container.GetItemQueryIterator<T>(queryDefinition);

        CancellationToken cancellationToken = _cancellationUtil.Get();

        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken);

            results.AddRange(response.ToList()); // TODO: I wonder if this is faster than foreach (response.Resource)

            if (delayMs != null)
                await Task.Delay(timeSpanDelay!.Value, cancellationToken: cancellationToken);
        }

        return results;
    }

    public virtual async ValueTask<List<TDocument>> GetItemsBetween(DateTime startAt, DateTime endAt, double? delayMs = null)
    {
        IQueryable<TDocument> query = await BuildQueryable();
        query = query.Where(c => c.CreatedAt >= startAt && c.CreatedAt <= endAt);

        List<TDocument> items = await GetItems(query, delayMs);

        return items;
    }
}
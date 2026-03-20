using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Soenneker.Constants.Data;
using Soenneker.Documents.Document;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask<(List<TDocument> items, string? continuationToken)> GetAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize,
        string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        // Build the query with paging and sorting
        IQueryable<TDocument> query = await BuildPagedQueryable(pageSize, continuationToken, cancellationToken)
            .NoSync();

        // OrderBy is required for paging
        query = query.OrderByDescending(static c => c.CreatedAt);

        // Directly return the result from GetItemsPaged
        return await GetItemsPaged(query, cancellationToken)
            .NoSync();
    }

    public virtual async ValueTask<(List<TDocument> items, string? continuationToken)> GetItemsPaged(QueryDefinition queryDefinition, int pageSize,
        string? continuationToken, CancellationToken cancellationToken = default)
    {
        if (_log && Logger.IsEnabled(LogLevel.Debug))
        {
            string query = BuildQueryLogText(queryDefinition);
            Logger.LogDebug("-- COSMOS: {method} ({type}): pageSize: {pageSize}, continuationToken: {token}, Query: {query}", MethodUtil.Get(),
                typeof(TDocument).Name, pageSize, continuationToken, query);
        }

        var requestOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize
        };

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        using FeedIterator<TDocument> iterator = container.GetItemQueryIterator<TDocument>(queryDefinition, continuationToken, requestOptions);

        if (!iterator.HasMoreResults)
            return ([], null);

        FeedResponse<TDocument> response = await iterator.ReadNextAsync(cancellationToken)
                                                         .NoSync();

        if (response.Count == 0)
            return ([], response.ContinuationToken);

        if (response.Resource is List<TDocument> list)
            return (list, response.ContinuationToken);

        var items = new List<TDocument>(response.Count);

        foreach (TDocument item in response)
        {
            items.Add(item);
        }

        return (items, response.ContinuationToken);
    }

    public virtual async ValueTask<(List<T> items, string? continuationToken)> GetItemsPaged<T>(IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        if (_log)
            LogQuery<T>(query, MethodUtil.Get());

        using FeedIterator<T> iterator = query.ToFeedIterator();

        if (!iterator.HasMoreResults)
            return ([], null);

        FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken)
                                                 .NoSync();

        if (response.Count == 0)
            return ([], response.ContinuationToken);

        if (response.Resource is List<T> list)
            return (list, response.ContinuationToken);

        var items = new List<T>(response.Count);
        foreach (T item in response)
        {
            items.Add(item);
        }

        return (items, response.ContinuationToken);
    }

    public virtual ValueTask<(List<TDocument> items, string? continuationToken)> GetItemsPaged(IQueryable<TDocument> query, int pageSize, string? continuation,
        CancellationToken cancellationToken = default)
    {
        return GetItemsPaged(query.ToQueryDefinition(), pageSize, continuation, cancellationToken);
    }
}
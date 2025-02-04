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
    public virtual async ValueTask<(List<TDocument>, string?)> GetAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        // Build the query with paging and sorting
        IQueryable<TDocument> query = await BuildPagedQueryable(pageSize, continuationToken, cancellationToken).NoSync();

        // OrderBy is required for paging
        query = query.OrderBy(c => c.CreatedAt);

        // Directly return the result from GetItemsPaged
        return await GetItemsPaged(query, cancellationToken).NoSync();
    }

    public virtual async ValueTask<(List<TDocument>, string?)> GetItemsPaged(QueryDefinition queryDefinition, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        // Logging only when enabled
        if (_log)
        {
            string query = BuildQueryLogText(queryDefinition);
            Logger.LogDebug(
                "-- COSMOS: {method} ({type}): pageSize: {pageSize}, continuationToken: {token}, Query: {query}",
                MethodUtil.Get(),
                typeof(TDocument).Name,
                pageSize,
                continuationToken,
                query);
        }

        // Set query request options
        var requestOptions = new QueryRequestOptions {MaxItemCount = pageSize};

        // Fetch the container once
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        // Create and execute the feed iterator
        using FeedIterator<TDocument> iterator = container.GetItemQueryIterator<TDocument>(queryDefinition, continuationToken, requestOptions);
        FeedResponse<TDocument> response = await iterator.ReadNextAsync(cancellationToken).NoSync();

        // Convert results
        return (response.ToList(), response.ContinuationToken);
    }

    public virtual async ValueTask<(List<T>, string?)> GetItemsPaged<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        if (_log)
            LogQuery<T>(query, MethodUtil.Get());

        // Execute the query and fetch results
        using FeedIterator<T> iterator = query.ToFeedIterator();
        FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken).NoSync();

        // Convert results
        return (response.ToList(), response.ContinuationToken);
    }
}
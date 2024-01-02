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
    public virtual async ValueTask<(List<TDocument>, string?)> GetAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null)
    {
        IQueryable<TDocument> queryable = await BuildPagedQueryable(pageSize, continuationToken).NoSync();

        // required for paging
        queryable = queryable.OrderBy(c => c.CreatedAt);

        (List<TDocument>, string?) result = await GetItemsPaged(queryable).NoSync();

        return result;
    }

    public virtual async ValueTask<(List<TDocument>, string?)> GetItemsPaged(QueryDefinition queryDefinition, int pageSize, string? continuationToken)
    {
        if (_log)
        {
            string query = BuildQueryLogText(queryDefinition);

            Logger.LogDebug("-- COSMOS: {method} ({type}): pageSize: {pageSize}, continuationToken: {token}, Query: {query}", MethodUtil.Get(), typeof(TDocument).Name, pageSize, continuationToken,
                query);
        }

        var requestOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize
        };

        Microsoft.Azure.Cosmos.Container container = await Container.NoSync();

        using FeedIterator<TDocument> iterator = container.GetItemQueryIterator<TDocument>(queryDefinition, continuationToken, requestOptions);

        CancellationToken cancellationToken = _cancellationUtil.Get();

        FeedResponse<TDocument> response = await iterator.ReadNextAsync(cancellationToken).NoSync();

        List<TDocument> results = response.ToList();

        return (results, response.ContinuationToken);
    }

    public virtual async ValueTask<(List<T> items, string? continuationToken)> GetItemsPaged<T>(IQueryable<T> queryable)
    {
        if (_log)
            LogQuery<T>(queryable, MethodUtil.Get());

        using FeedIterator<T> iterator = queryable.ToFeedIterator();

        CancellationToken cancellationToken = _cancellationUtil.Get();

        FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken).NoSync();

        List<T> results = response.ToList();

        return (results, response.ContinuationToken);
    }
}
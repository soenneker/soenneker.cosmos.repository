using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Documents.Document;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

//references to documentation about cosmos linq to sql and cosmos linq query 
//https://docs.microsoft.com/en-us/azure/cosmos-db/sql/sql-query-linq-to-sql
//https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.container.getitemlinqquery?view=azure-dotnet

/// <summary>
/// Represents the cosmos repository.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    /// <summary>
    /// Builds queryable.
    /// </summary>
    /// <param name="queryRequestOptions">The query request options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IQueryable<TDocument>> BuildQueryable(QueryRequestOptions? queryRequestOptions = null, CancellationToken cancellationToken = default)
    {
        return BuildQueryable<TDocument>(queryRequestOptions, cancellationToken);
    }

    /// <summary>
    /// Builds queryable.
    /// </summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="queryRequestOptions">The query request options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<IQueryable<T>> BuildQueryable<T>(QueryRequestOptions? queryRequestOptions = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();
        return container.GetItemLinqQueryable<T>(requestOptions: queryRequestOptions);
    }

    /// <summary>
    /// Builds paged queryable.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="continuationToken">The continuation token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IQueryable<TDocument>> BuildPagedQueryable(int pageSize = 500, string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        return BuildPagedQueryable<TDocument>(pageSize, continuationToken, cancellationToken);
    }

    /// <summary>
    /// Builds paged queryable.
    /// </summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="pageSize">The page size.</param>
    /// <param name="continuationToken">The continuation token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public ValueTask<IQueryable<T>> BuildPagedQueryable<T>(int pageSize = 500, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        var requestOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize
        };

        return BuildPagedQueryableCore(this, requestOptions, continuationToken, cancellationToken);

        static async ValueTask<IQueryable<T>> BuildPagedQueryableCore(CosmosRepository<TDocument> repo, QueryRequestOptions requestOptions,
            string? continuationToken, CancellationToken cancellationToken)
        {
            Microsoft.Azure.Cosmos.Container container = await repo.Container(cancellationToken)
                                                                   .NoSync();
            return container.GetItemLinqQueryable<T>(continuationToken: continuationToken, requestOptions: requestOptions);
        }
    }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<int> Count(CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(null, cancellationToken)
            .NoSync();

        return await Count(query, cancellationToken)
            .NoSync();
    }

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<int> Count(IQueryable<TDocument> query, CancellationToken cancellationToken = default)
    {
        Response<int> response = await query.CountAsync(cancellationToken: cancellationToken)
                                            .NoSync();

        return response.Resource;
    }

    /// <summary>
    /// Executes the any operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<bool> Any(CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(null, cancellationToken)
            .NoSync();

        return await Exists(query, cancellationToken)
            .NoSync();
    }

    /// <summary>
    /// Executes the none operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<bool> None(CancellationToken cancellationToken = default)
    {
        return !await Any(cancellationToken)
            .NoSync();
    }

    /// <summary>
    /// Gets item.
    /// </summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<T?> GetItem<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        LogQuery<T>(query, MethodUtil.Get());

        using FeedIterator<T> iterator = query.Take(1)
                                              .ToFeedIterator();

        if (!iterator.HasMoreResults)
            return default;

        FeedResponse<T> page = await iterator.ReadNextAsync(cancellationToken)
                                             .NoSync();

        if (page.Count == 0)
            return default;

        if (page.Resource is IReadOnlyList<T> list)
            return list[0];

        using IEnumerator<T> enumerator = page.Resource.GetEnumerator();
        return enumerator.MoveNext() ? enumerator.Current : default;
    }

    /// <summary>
    /// Gets items.
    /// </summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<List<T>> GetItems<T>(IQueryable<T> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        LogQuery<T>(query, MethodUtil.Get());

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        using FeedIterator<T> iterator = query.ToFeedIterator();

        return await DrainIterator(iterator, delay, cancellationToken)
            .NoSync();
    }

    /// <summary>
    /// Gets items.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<List<TDocument>> GetItems(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        LogQuery<TDocument>(query, MethodUtil.Get());

        using FeedIterator<TDocument>? iterator = query.ToFeedIterator();
        return await DrainIterator(iterator, delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null, cancellationToken)
            .NoSync();
    }
}
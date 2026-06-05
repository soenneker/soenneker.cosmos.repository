using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Constants.Data;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the cosmos repository contract.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Builds queryable.
    /// </summary>
    /// <param name="queryRequestOptions">The query request options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<IQueryable<TDocument>> BuildQueryable(QueryRequestOptions? queryRequestOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds queryable.
    /// </summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="queryRequestOptions">The query request options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<IQueryable<T>> BuildQueryable<T>(QueryRequestOptions? queryRequestOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds paged queryable.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="continuationToken">The continuation token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<IQueryable<TDocument>> BuildPagedQueryable(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an empty query that can utilize LINQ, specifying the Cosmos requestOptions. Does not actually query. <para/>
    /// Be sure to order in your query. Leverage QueryableExtension.ToOrdered{IQueryable}/>
    /// </summary>
    [Pure]
    ValueTask<IQueryable<T>> BuildPagedQueryable<T>(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Essentially wraps <see cref="GetItems{T}(string, double?, CancellationToken)"/> with .FirstOrDefault()
    /// </summary>
    [Pure]
    ValueTask<T?> GetItem<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Will always return a non-null list. It may or may not have items.
    /// </summary>
    [Pure]
    ValueTask<List<T>> GetItems<T>(IQueryable<T> query, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Will always return a non-null list. It may or may not have items.
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetItems(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<int> Count(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the count operation.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<int> Count(IQueryable<TDocument> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the any operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<bool> Any(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the none operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<bool> None(CancellationToken cancellationToken = default);
}
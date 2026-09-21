using Soenneker.Cosmos.Repository.Dtos;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Constants.Data;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines linq operations for Cosmos DB documents.
/// </summary>
/// <remarks>
/// Methods that execute an existing IQueryable use the request options attached when the query was built.
/// Configure consistency and session tokens through BuildQueryable's request options or the builders' readOptions.
/// </remarks>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Builds queryable.
    /// </summary>
    /// <param name="queryRequestOptions">Explicit SDK options. When supplied, these take precedence over readOptions and repository defaults.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="readOptions">Overrides repository read defaults when SDK options are omitted. An explicit empty value restores SDK defaults.</param>
    /// <returns>A task whose result is the requested queryable.</returns>
    [Pure]
    ValueTask<IQueryable<TDocument>> BuildQueryable(QueryRequestOptions? queryRequestOptions = null,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds queryable.
    /// </summary>
    /// <typeparam name="T">Type of value handled by the cosmos repository.</typeparam>
    /// <param name="queryRequestOptions">Explicit SDK options. When supplied, these take precedence over readOptions and repository defaults.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="readOptions">Overrides repository read defaults when SDK options are omitted. An explicit empty value restores SDK defaults.</param>
    /// <returns>A task whose result is the requested queryable.</returns>
    [Pure]
    ValueTask<IQueryable<T>> BuildQueryable<T>(QueryRequestOptions? queryRequestOptions = null,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds paged queryable.
    /// </summary>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="continuationToken">Token identifying the next page of query results.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="readOptions">Overrides repository read defaults. Null inherits them; an explicit empty value restores SDK defaults.</param>
    /// <returns>A task whose result is the requested queryable.</returns>
    [Pure]
    ValueTask<IQueryable<TDocument>> BuildPagedQueryable(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an empty query that can utilize LINQ, specifying the Cosmos requestOptions. Does not actually query. <para/>
    /// Be sure to order in your query. Leverage QueryableExtension.ToOrdered{IQueryable}/>
    /// </summary>
    /// <typeparam name="T">Type of value handled by the cosmos repository.</typeparam>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="continuationToken">Token identifying the next page of query results.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="readOptions">Overrides repository read defaults. Null inherits them; an explicit empty value restores SDK defaults.</param>
    /// <returns>A task whose result is the requested queryable.</returns>
    [Pure]
    ValueTask<IQueryable<T>> BuildPagedQueryable<T>(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Essentially wraps <see cref="GetItems{T}(string, double?, CosmosReadOptions?, CancellationToken)"/> with .FirstOrDefault()
    /// </summary>
    /// <typeparam name="T">Type of value handled by the cosmos repository.</typeparam>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the value returned by get Item.</returns>
    [Pure]
    ValueTask<T?> GetItem<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Will always return a non-null list. It may or may not have items.
    /// </summary>
    /// <typeparam name="T">Type of value handled by the cosmos repository.</typeparam>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Items.</returns>
    [Pure]
    ValueTask<List<T>> GetItems<T>(IQueryable<T> query, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Will always return a non-null list. It may or may not have items.
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Items.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetItems(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts cosmos Repository.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="readOptions">Overrides repository read defaults. Null inherits them; an explicit empty value restores SDK defaults.</param>
    /// <returns>A task whose result is the requested value.</returns>
    [Pure]
    ValueTask<int> Count(CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts cosmos Repository.
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the requested value.</returns>
    [Pure]
    ValueTask<int> Count(IQueryable<TDocument> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for cosmos Repository.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="readOptions">Overrides repository read defaults. Null inherits them; an explicit empty value restores SDK defaults.</param>
    /// <returns>true if retrieves any from the Cosmos Repository; otherwise, false.</returns>
    [Pure]
    ValueTask<bool> Any(CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the value produced by none.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="readOptions">Overrides repository read defaults. Null inherits them; an explicit empty value restores SDK defaults.</param>
    /// <returns>true if retrieves none from the Cosmos Repository; otherwise, false.</returns>
    [Pure]
    ValueTask<bool> None(CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default);
}

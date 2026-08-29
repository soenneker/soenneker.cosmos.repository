using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Constants.Data;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Gets all paged.
    /// </summary>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="continuationToken">Token identifying the next page of query results.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the requested (List items, string continuation Token).</returns>
    [Pure]
    ValueTask<(List<TDocument> items, string? continuationToken)> GetAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of items using a query definition and continuation token.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <param name="pageSize">The maximum number of items in the page.</param>
    /// <param name="continuationToken">The continuation token from the previous page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The items in the page and the continuation token for the next page.</returns>
    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    [Pure]
    ValueTask<(List<TDocument> items, string? continuationToken)> GetItemsPaged(QueryDefinition queryDefinition, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Be sure to pass a query that was built via <see cref="BuildPagedQueryable"/>
    /// </summary>
    /// <typeparam name="T">Type of value handled by the cosmos repository.</typeparam>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the requested (List items, string continuation Token).</returns>
    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    [Pure]
    ValueTask<(List<T> items, string? continuationToken)> GetItemsPaged<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items paged.
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="continuation">Continuation for the get items paged operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the requested (List items, string continuation Token).</returns>
    [Pure]
    ValueTask<(List<TDocument> items, string? continuationToken)> GetItemsPaged(IQueryable<TDocument> query, int pageSize, string? continuation,
        CancellationToken cancellationToken = default);
}

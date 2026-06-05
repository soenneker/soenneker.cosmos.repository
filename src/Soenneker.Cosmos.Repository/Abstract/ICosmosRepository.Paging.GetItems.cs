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
    /// Gets all paged.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="continuationToken">The continuation token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<(List<TDocument> items, string? continuationToken)> GetAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null, CancellationToken cancellationToken = default);

    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    [Pure]
    ValueTask<(List<TDocument> items, string? continuationToken)> GetItemsPaged(QueryDefinition queryDefinition, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Be sure to pass a query that was built via <see cref="BuildPagedQueryable"/>
    /// </summary>
    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    [Pure]
    ValueTask<(List<T> items, string? continuationToken)> GetItemsPaged<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items paged.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="continuation">The continuation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<(List<TDocument> items, string? continuationToken)> GetItemsPaged(IQueryable<TDocument> query, int pageSize, string? continuation,
        CancellationToken cancellationToken = default);
}
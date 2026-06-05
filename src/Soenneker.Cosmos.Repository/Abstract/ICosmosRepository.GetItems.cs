using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Dtos.IdNamePair;
using Soenneker.Dtos.IdPartitionPair;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the cosmos repository contract.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Careful, could be heavy. You may want <see cref="GetAllPaged"/> if the number of items are large (due to app memory limitations)
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetAll(double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Careful, could be heavy. You may want <see cref="GetAllPaged"/> if the number of items are large (due to app memory limitations)
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetAllByPartitionKey(string partitionKey, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get items given a string SQL query directly. Typically should avoid (use specification, parameterization concerns, etc)
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetItems(string query, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Not recommended - fans out across all partitions, so can be slow and expensive.
    /// </summary>
    /// <param name="ids"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Pure]
    ValueTask<List<TDocument>> GetAllByDocumentIds(List<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all by id partition pairs.
    /// </summary>
    /// <param name="pairs">The pairs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetAllByIdPartitionPairs(List<IdPartitionPair> pairs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all by id name pairs.
    /// </summary>
    /// <param name="pairs">The pairs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetAllByIdNamePairs(List<IdNamePair> pairs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items.
    /// </summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<List<T>> GetItems<T>(string query, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetItems(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The bottom method call for most GetItems() in ICosmosRepository
    /// </summary>
    [Pure]
    ValueTask<List<T>> GetItems<T>(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of items with createdAt between the parameters (inclusive, careful). Non-ordered.
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetItemsBetween(DateTimeOffset startAt, DateTimeOffset endAt, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets ids.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <param name="options">The options.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<List<IdPartitionPair>> GetIds(QueryDefinition queryDefinition, QueryRequestOptions? options = null, double? delayMs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all ids.
    /// </summary>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<List<IdPartitionPair>> GetAllIds(double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Before executing, adds an additional where clause to only gather ids from a given query (useful say during deletion)
    /// </summary>
    [Pure]
    ValueTask<List<IdPartitionPair>> GetIds(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all partition keys.
    /// </summary>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<List<string>> GetAllPartitionKeys(double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Before executing, adds an additional where clause to only gather partitionKeys from a given query
    /// </summary>
    [Pure]
    ValueTask<List<string>> GetPartitionKeys(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default);
}
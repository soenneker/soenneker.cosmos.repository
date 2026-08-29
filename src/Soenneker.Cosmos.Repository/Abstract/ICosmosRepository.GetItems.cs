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

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Careful, could be heavy. You may want <see cref="GetAllPaged"/> if the number of items are large (due to app memory limitations)
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get All.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetAll(double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Careful, could be heavy. You may want <see cref="GetAllPaged"/> if the number of items are large (due to app memory limitations)
    /// </summary>
    /// <param name="partitionKey">Partition key used to route the database operation.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get All By Partition Key.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetAllByPartitionKey(string partitionKey, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get items given a string SQL query directly. Typically should avoid (use specification, parameterization concerns, etc)
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Items.</returns>
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
    /// <param name="pairs">pairs to process.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get All By ID Partition Pairs.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetAllByIdPartitionPairs(List<IdPartitionPair> pairs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all by id name pairs.
    /// </summary>
    /// <param name="pairs">pairs to process.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get All By ID Name Pairs.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetAllByIdNamePairs(List<IdNamePair> pairs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items.
    /// </summary>
    /// <typeparam name="T">Type of value handled by the cosmos repository.</typeparam>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Items.</returns>
    [Pure]
    ValueTask<List<T>> GetItems<T>(string query, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items.
    /// </summary>
    /// <param name="queryDefinition">Database query and parameters to execute.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Items.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetItems(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The bottom method call for most GetItems() in ICosmosRepository
    /// </summary>
    /// <typeparam name="T">Type of value handled by the cosmos repository.</typeparam>
    /// <param name="queryDefinition">Database query and parameters to execute.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Items.</returns>
    [Pure]
    ValueTask<List<T>> GetItems<T>(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of items with createdAt between the parameters (inclusive, careful). Non-ordered.
    /// </summary>
    /// <param name="startAt">Start At for the get items between operation.</param>
    /// <param name="endAt">End At for the get items between operation.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Items Between.</returns>
    [Pure]
    ValueTask<List<TDocument>> GetItemsBetween(DateTimeOffset startAt, DateTimeOffset endAt, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets ids.
    /// </summary>
    /// <param name="queryDefinition">Database query and parameters to execute.</param>
    /// <param name="options">Options to configure for the cosmos repository.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Ids.</returns>
    [Pure]
    ValueTask<List<IdPartitionPair>> GetIds(QueryDefinition queryDefinition, QueryRequestOptions? options = null, double? delayMs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all ids.
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get All Ids.</returns>
    [Pure]
    ValueTask<List<IdPartitionPair>> GetAllIds(double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Before executing, adds an additional where clause to only gather ids from a given query (useful say during deletion)
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Ids.</returns>
    [Pure]
    ValueTask<List<IdPartitionPair>> GetIds(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all partition keys.
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get All Partition Keys.</returns>
    [Pure]
    ValueTask<List<string>> GetAllPartitionKeys(double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Before executing, adds an additional where clause to only gather partitionKeys from a given query
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by get Partition Keys.</returns>
    [Pure]
    ValueTask<List<string>> GetPartitionKeys(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default);
}

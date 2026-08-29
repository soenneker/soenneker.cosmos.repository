using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Dtos.IdPartitionPair;
using Soenneker.Cosmos.Repository.Dtos;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines delete operations for Cosmos DB documents.
/// </summary>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Deletes a wrapped item only when its ETag still matches.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    /// <param name="item">Receives the entry when the key is found.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteItemIfMatch(CosmosItem<TDocument> item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard deletes one item by Id (partition and document, or one guid if they're the same).
    /// Will not throw.
    /// </summary>
    /// <param name="entityId">Identifier of the entity to target.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the item deletion is complete.</returns>
    ValueTask DeleteItem(string entityId, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an item only when its current ETag matches <paramref name="expectedETag"/>.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    /// <param name="entityId">Identifier of the entity to target.</param>
    /// <param name="expectedETag">ETag required for the conditional update.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteItemIfMatch(string entityId, string expectedETag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes item.
    /// </summary>
    /// <param name="documentId">Identifier of the target document.</param>
    /// <param name="partitionKey">Partition key used to route the database operation.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteItem(string documentId, string partitionKey, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an item only when its current ETag matches <paramref name="expectedETag"/>.
    /// </summary>
    /// <param name="documentId">Identifier of the target document.</param>
    /// <param name="partitionKey">Partition key used to route the database operation.</param>
    /// <param name="expectedETag">ETag required for the conditional update.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteItemIfMatch(string documentId, string partitionKey, string expectedETag,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all items.
    /// </summary>
    /// <param name="delayMs">The optional delay between delete operations, in milliseconds.</param>
    /// <param name="useQueue">Whether to enqueue the delete operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>TODO: Perhaps want to turn on Bulk support https://devblogs.microsoft.com/cosmosdb/introducing-bulk-support-in-the-net-sdk/</remarks>
    ValueTask DeleteAll(double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes items.
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteItems(IQueryable<TDocument> query, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes items parallel.
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="maxConcurrency">Maximum number of operations allowed to run concurrently.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteItemsParallel(IQueryable<TDocument> query, int maxConcurrency, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes ids.
    /// </summary>
    /// <param name="ids">Identifiers of the target entries.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteIds(List<IdPartitionPair> ids, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every item only when its current ETag matches the value keyed by its full ID.
    /// </summary>
    /// <param name="ids">Identifiers of the target entries.</param>
    /// <param name="expectedETags">expected E Tags to process.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteIdsIfMatch(List<IdPartitionPair> ids, IReadOnlyDictionary<string, string> expectedETags, double? delayMs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes ids parallel.
    /// </summary>
    /// <param name="ids">Identifiers of the target entries.</param>
    /// <param name="maxConcurrency">Maximum number of operations allowed to run concurrently.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteIdsParallel(List<IdPartitionPair> ids, int maxConcurrency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every item in parallel only when its current ETag matches the value keyed by its full ID.
    /// </summary>
    /// <param name="ids">Identifiers of the target entries.</param>
    /// <param name="expectedETags">expected E Tags to process.</param>
    /// <param name="maxConcurrency">Maximum number of operations allowed to run concurrently.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteIdsParallelIfMatch(List<IdPartitionPair> ids, IReadOnlyDictionary<string, string> expectedETags, int maxConcurrency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes created at between.
    /// </summary>
    /// <param name="startAt">Start At for the delete created at between operation.</param>
    /// <param name="endAt">End At for the delete created at between operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteCreatedAtBetween(DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken = default);
}

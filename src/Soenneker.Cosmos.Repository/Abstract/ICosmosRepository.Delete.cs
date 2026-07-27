using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Dtos.IdPartitionPair;
using Soenneker.Cosmos.Repository.Dtos;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Deletes a wrapped item only when its ETag still matches.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    ValueTask DeleteItemIfMatch(CosmosItem<TDocument> item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard deletes one item by Id (partition and document, or one guid if they're the same).
    /// Will not throw.
    /// </summary>
    ValueTask DeleteItem(string entityId, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an item only when its current ETag matches <paramref name="expectedETag"/>.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    ValueTask DeleteItemIfMatch(string entityId, string expectedETag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes item.
    /// </summary>
    /// <param name="documentId">The document id.</param>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteItem(string documentId, string partitionKey, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an item only when its current ETag matches <paramref name="expectedETag"/>.
    /// </summary>
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
    /// <param name="query">The query.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteItems(IQueryable<TDocument> query, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes items parallel.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="maxConcurrency">The max concurrency.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteItemsParallel(IQueryable<TDocument> query, int maxConcurrency, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes ids.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteIds(List<IdPartitionPair> ids, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every item only when its current ETag matches the value keyed by its full ID.
    /// </summary>
    ValueTask DeleteIdsIfMatch(List<IdPartitionPair> ids, IReadOnlyDictionary<string, string> expectedETags, double? delayMs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes ids parallel.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="maxConcurrency">The max concurrency.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteIdsParallel(List<IdPartitionPair> ids, int maxConcurrency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every item in parallel only when its current ETag matches the value keyed by its full ID.
    /// </summary>
    ValueTask DeleteIdsParallelIfMatch(List<IdPartitionPair> ids, IReadOnlyDictionary<string, string> expectedETags, int maxConcurrency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes created at between.
    /// </summary>
    /// <param name="startAt">The start at.</param>
    /// <param name="endAt">The end at.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteCreatedAtBetween(DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken = default);
}

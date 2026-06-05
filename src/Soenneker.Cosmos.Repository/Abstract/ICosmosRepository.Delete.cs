using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Dtos.IdPartitionPair;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the cosmos repository contract.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Hard deletes one item by Id (partition and document, or one guid if they're the same).
    /// Will not throw.
    /// </summary>
    ValueTask DeleteItem(string entityId, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes item.
    /// </summary>
    /// <param name="documentId">The document id.</param>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteItem(string documentId, string partitionKey, bool useQueue = false, CancellationToken cancellationToken = default);

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
    /// Deletes ids parallel.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="maxConcurrency">The max concurrency.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteIdsParallel(List<IdPartitionPair> ids, int maxConcurrency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes created at between.
    /// </summary>
    /// <param name="startAt">The start at.</param>
    /// <param name="endAt">The end at.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteCreatedAtBetween(DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken cancellationToken = default);
}
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Cosmos.Repository.Dtos;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines update items operations for Cosmos DB documents.
/// </summary>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Updates items.
    /// </summary>
    /// <param name="documents">Documents to index, query, or transform.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="excludeResponse">exclude Response returned by the upstream operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by update Items.</returns>
    ValueTask<List<TDocument>> UpdateItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates every wrapped document only when its current ETag matches and returns each new ETag.
    /// </summary>
    /// <param name="items">items to inspect or update.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by update Items If Match.</returns>
    ValueTask<List<CosmosItem<TDocument>>> UpdateItemsIfMatch(List<CosmosItem<TDocument>> items, double? delayMs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates items parallel.
    /// </summary>
    /// <param name="documents">Documents to index, query, or transform.</param>
    /// <param name="maxConcurrency">Maximum number of operations allowed to run concurrently.</param>
    /// <param name="excludeResponse">exclude Response returned by the upstream operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by update Items Parallel.</returns>
    ValueTask<List<TDocument>> UpdateItemsParallel(List<TDocument> documents, int maxConcurrency, bool excludeResponse = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates every wrapped document in parallel only when its current ETag matches and returns each new ETag.
    /// </summary>
    /// <param name="items">items to inspect or update.</param>
    /// <param name="maxConcurrency">Maximum number of operations allowed to run concurrently.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by update Items Parallel If Match.</returns>
    ValueTask<List<CosmosItem<TDocument>>> UpdateItemsParallelIfMatch(List<CosmosItem<TDocument>> items, int maxConcurrency,
        CancellationToken cancellationToken = default);
}

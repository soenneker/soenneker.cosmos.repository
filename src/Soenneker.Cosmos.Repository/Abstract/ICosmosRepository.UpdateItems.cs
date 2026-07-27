using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Cosmos.Repository.Dtos;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Updates items.
    /// </summary>
    /// <param name="documents">The documents.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="excludeResponse">The exclude response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    ValueTask<List<TDocument>> UpdateItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates every wrapped document only when its current ETag matches and returns each new ETag.
    /// </summary>
    ValueTask<List<CosmosItem<TDocument>>> UpdateItemsIfMatch(List<CosmosItem<TDocument>> items, double? delayMs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates items parallel.
    /// </summary>
    /// <param name="documents">The documents.</param>
    /// <param name="maxConcurrency">The max concurrency.</param>
    /// <param name="excludeResponse">The exclude response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    ValueTask<List<TDocument>> UpdateItemsParallel(List<TDocument> documents, int maxConcurrency, bool excludeResponse = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates every wrapped document in parallel only when its current ETag matches and returns each new ETag.
    /// </summary>
    ValueTask<List<CosmosItem<TDocument>>> UpdateItemsParallelIfMatch(List<CosmosItem<TDocument>> items, int maxConcurrency,
        CancellationToken cancellationToken = default);
}

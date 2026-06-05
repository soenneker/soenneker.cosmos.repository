using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the cosmos repository contract.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
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
    /// Updates items parallel.
    /// </summary>
    /// <param name="documents">The documents.</param>
    /// <param name="maxConcurrency">The max concurrency.</param>
    /// <param name="excludeResponse">The exclude response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    ValueTask<List<TDocument>> UpdateItemsParallel(List<TDocument> documents, int maxConcurrency, bool excludeResponse = false, CancellationToken cancellationToken = default);
}
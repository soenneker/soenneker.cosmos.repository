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
    /// Essentially just a helper that iterates over a list, calling <see cref="AddItem"/>
    /// </summary>
    ValueTask<List<TDocument>> AddItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds items parallel.
    /// </summary>
    /// <param name="documents">The documents.</param>
    /// <param name="maxConcurrency">The max concurrency.</param>
    /// <param name="excludeResponse">The exclude response.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    ValueTask<List<TDocument>> AddItemsParallel(List<TDocument> documents, int maxConcurrency, bool excludeResponse = false, CancellationToken cancellationToken = default);
}
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Essentially just a helper that iterates over a list, calling <see cref="AddItem"/>
    /// </summary>
    /// <param name="documents">Documents to index, query, or transform.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="excludeResponse">exclude Response returned by the upstream operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by add Items.</returns>
    ValueTask<List<TDocument>> AddItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds items parallel.
    /// </summary>
    /// <param name="documents">Documents to index, query, or transform.</param>
    /// <param name="maxConcurrency">Maximum number of operations allowed to run concurrently.</param>
    /// <param name="excludeResponse">exclude Response returned by the upstream operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by add Items Parallel.</returns>
    ValueTask<List<TDocument>> AddItemsParallel(List<TDocument> documents, int maxConcurrency, bool excludeResponse = false, CancellationToken cancellationToken = default);
}

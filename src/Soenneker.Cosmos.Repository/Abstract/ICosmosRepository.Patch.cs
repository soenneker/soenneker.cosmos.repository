using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Cosmos.Repository.Dtos;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Patches a wrapped item only when its ETag still matches.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    /// <returns>The patched document and its new ETag.</returns>
    ValueTask<CosmosItem<TDocument>> PatchItemIfMatch(CosmosItem<TDocument> item, List<PatchOperation> operations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the patch items operation.
    /// </summary>
    /// <param name="documents">The documents.</param>
    /// <param name="operations">The operations.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    ValueTask<List<TDocument>> PatchItems(List<TDocument> documents, List<PatchOperation> operations, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches every wrapped document only when its current ETag matches and returns each new ETag.
    /// </summary>
    ValueTask<List<CosmosItem<TDocument>>> PatchItemsIfMatch(List<CosmosItem<TDocument>> items, List<PatchOperation> operations,
        double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the patch item operation.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="operations">The operations.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    ValueTask<TDocument?> PatchItem(string id, List<PatchOperation> operations, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches an item only when its current ETag matches <paramref name="expectedETag"/>.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    /// <returns>The patched document and its new ETag.</returns>
    ValueTask<CosmosItem<TDocument>> PatchItemIfMatch(string id, List<PatchOperation> operations, string expectedETag,
        CancellationToken cancellationToken = default);
}

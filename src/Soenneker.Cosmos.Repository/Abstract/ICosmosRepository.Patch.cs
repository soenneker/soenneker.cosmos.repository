using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Cosmos.Repository.Dtos;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines patch operations for Cosmos DB documents.
/// </summary>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Patches a wrapped item only when its ETag still matches.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    /// <param name="item">Receives the entry when the key is found.</param>
    /// <param name="operations">Operations to execute, in order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The patched document and its new ETag.</returns>
    ValueTask<CosmosItem<TDocument>> PatchItemIfMatch(CosmosItem<TDocument> item, List<PatchOperation> operations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches items.
    /// </summary>
    /// <param name="documents">Documents to index, query, or transform.</param>
    /// <param name="operations">Operations to execute, in order.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by patch Items.</returns>
    ValueTask<List<TDocument>> PatchItems(List<TDocument> documents, List<PatchOperation> operations, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches every wrapped document only when its current ETag matches and returns each new ETag.
    /// </summary>
    /// <param name="items">items to inspect or update.</param>
    /// <param name="operations">Operations to execute, in order.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the collection returned by patch Items If Match.</returns>
    ValueTask<List<CosmosItem<TDocument>>> PatchItemsIfMatch(List<CosmosItem<TDocument>> items, List<PatchOperation> operations,
        double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches item.
    /// </summary>
    /// <param name="id">Identifier of the cosmos repository instance or registration to target.</param>
    /// <param name="operations">Operations to execute, in order.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the t Document returned by patch Item.</returns>
    ValueTask<TDocument?> PatchItem(string id, List<PatchOperation> operations, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches an item only when its current ETag matches <paramref name="expectedETag"/>.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    /// <param name="id">Identifier of the Cosmos Repository instance or registration to target.</param>
    /// <param name="operations">Operations to execute, in order.</param>
    /// <param name="expectedETag">ETag required for the conditional update.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The patched document and its new ETag.</returns>
    ValueTask<CosmosItem<TDocument>> PatchItemIfMatch(string id, List<PatchOperation> operations, string expectedETag,
        CancellationToken cancellationToken = default);
}

using System.Threading;
using System.Threading.Tasks;
using Soenneker.Cosmos.Repository.Dtos;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Updates a wrapped item only when its ETag still matches.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    /// <param name="item">Receives the entry when the key is found.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The updated document and its new ETag.</returns>
    ValueTask<CosmosItem<TDocument>> UpdateItemIfMatch(CosmosItem<TDocument> item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an item only when its current ETag matches <paramref name="expectedETag"/>.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    /// <param name="document">Document to read, persist, or update.</param>
    /// <param name="expectedETag">ETag required for the conditional update.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The updated document and its new ETag.</returns>
    ValueTask<CosmosItem<TDocument>> UpdateItemIfMatch(TDocument document, string expectedETag,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an item only when its current ETag matches <paramref name="expectedETag"/>.
    /// Cosmos DB throws a 412 Precondition Failed response when the item has changed.
    /// </summary>
    /// <param name="id">Identifier of the Cosmos Repository instance or registration to target.</param>
    /// <param name="document">Document to read, persist, or update.</param>
    /// <param name="expectedETag">ETag required for the conditional update.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The updated document and its new ETag.</returns>
    ValueTask<CosmosItem<TDocument>> UpdateItemIfMatch(string id, TDocument document, string expectedETag,
        CancellationToken cancellationToken = default);

    // TODO: Add ModifiedAt within this method
    /// <summary>
    /// Updates an item unconditionally.
    /// </summary>
    /// <param name="document">The document to replace.</param>
    /// <param name="useQueue">Whether to enqueue the update.</param>
    /// <param name="excludeResponse">Whether Cosmos DB should omit the response body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document, or the supplied document when the response body is excluded or the operation is queued.</returns>
    ValueTask<TDocument> UpdateItem(TDocument document, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an item with the specified full identifier unconditionally.
    /// </summary>
    /// <param name="id">The full item identifier.</param>
    /// <param name="document">The document to replace.</param>
    /// <param name="useQueue">Whether to enqueue the update.</param>
    /// <param name="excludeResponse">Whether Cosmos DB should omit the response body.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document, or the supplied document when the response body is excluded or the operation is queued.</returns>
    ValueTask<TDocument> UpdateItem(string id, TDocument document, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default);
}

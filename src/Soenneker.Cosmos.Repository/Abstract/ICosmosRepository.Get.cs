using Soenneker.Dtos.IdNamePair;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Cosmos.Repository.Dtos;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines get operations for Cosmos DB documents.
/// </summary>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Gets an item together with the ETag required for a subsequent conditional write.
    /// </summary>
    /// <param name="id">Identifier of the cosmos repository instance or registration to target.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the requested cosmos Item.</returns>
    ValueTask<CosmosItem<TDocument>?> GetItemWithETag(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an item together with the ETag required for a subsequent conditional write.
    /// </summary>
    /// <param name="documentId">Identifier of the target document.</param>
    /// <param name="partitionKey">Partition key used to route the database operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the requested cosmos Item.</returns>
    ValueTask<CosmosItem<TDocument>?> GetItemWithETag(string documentId, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one item by full ID (partition key and document ID, or one value when they are the same).
    /// </summary>
    /// <param name="id">Identifier of the Cosmos Repository instance or registration to target.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The document, or <see langword="null"/> when Cosmos returns 404 Not Found. Other failures propagate.</returns>
    [Pure]
    ValueTask<TDocument?> GetItem(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an arbitrary first document from the specified partition.
    /// </summary>
    /// <param name="partitionKey"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The document, or <see langword="null"/> when the partition is empty.</returns>
    [Pure]
    ValueTask<TDocument?> GetItemByPartitionKey(string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent document associated with the specified partition key, if available.
    /// </summary>
    /// <param name="partitionKey">The partition key used to identify the set of documents to search. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task that represents the asynchronous operation. The result contains the latest document for the
    /// specified partition key, or null if no document exists.</returns>
    [Pure]
    ValueTask<TDocument?> GetLatestByPartitionKey(string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets item by id name pair.
    /// </summary>
    /// <param name="idNamePair">Id Name Pair for the get item by id name pair operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the t Document returned by get Item By ID Name Pair.</returns>
    [Pure]
    ValueTask<TDocument?> GetItemByIdNamePair(IdNamePair idNamePair, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one item by its document ID and partition key.
    /// </summary>
    /// <param name="documentId">Identifier of the target document.</param>
    /// <param name="partitionKey">Partition key used to route the database operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The document, or <see langword="null"/> when Cosmos returns 404 Not Found. Other failures propagate.</returns>
    [Pure]
    ValueTask<TDocument?> GetItem(string documentId, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first item ordered by creation time ascending.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The earliest-created item, or <see langword="null"/> when no item exists.</returns>
    [Pure]
    ValueTask<TDocument?> GetFirst(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first item ordered by creation time descending.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The latest-created item, or <see langword="null"/> when no item exists.</returns>
    [Pure]
    ValueTask<TDocument?> GetLast(CancellationToken cancellationToken = default);
}

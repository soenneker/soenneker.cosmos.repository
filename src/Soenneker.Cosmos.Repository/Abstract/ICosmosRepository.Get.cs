using Soenneker.Dtos.IdNamePair;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Cosmos.Repository.Dtos;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Gets an item together with the ETag required for a subsequent conditional write.
    /// </summary>
    ValueTask<CosmosItem<TDocument>?> GetItemWithETag(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an item together with the ETag required for a subsequent conditional write.
    /// </summary>
    ValueTask<CosmosItem<TDocument>?> GetItemWithETag(string documentId, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get one item by Id (partition id and document id, or one guid if they're the same) <para/>
    /// Will not throw.
    /// </summary>
    /// <returns>null if cannot be found</returns>
    [Pure]
    ValueTask<TDocument?> GetItem(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves document(s) by partitionKey, and then executes .FirstOrDefault(). The assumption is there's only one document by the partition key specified. <para/>
    /// Will not throw.
    /// </summary>
    /// <param name="partitionKey"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>null if cannot be found</returns>
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
    /// <param name="idNamePair">The id name pair.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<TDocument?> GetItemByIdNamePair(IdNamePair idNamePair, CancellationToken cancellationToken = default);

    /// <summary>
    /// Will not throw.
    /// </summary>
    /// <returns>null if cannot be found</returns>
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

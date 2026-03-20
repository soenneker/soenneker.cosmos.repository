using Soenneker.Dtos.IdNamePair;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
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

    [Pure]
    ValueTask<TDocument?> GetItemByIdNamePair(IdNamePair idNamePair, CancellationToken cancellationToken = default);

    /// <summary>
    /// Will not throw.
    /// </summary>
    /// <returns>null if cannot be found</returns>
    [Pure]
    ValueTask<TDocument?> GetItem(string documentId, string partitionKey, CancellationToken cancellationToken = default);

    /// <returns>
    /// The very first item ordered by createdAt ascending
    /// </returns>
    [Pure]
    ValueTask<TDocument?> GetFirst(CancellationToken cancellationToken = default);

    /// <returns>
    /// The very first item ordered by createdAt descending
    /// </returns>
    [Pure]
    ValueTask<TDocument?> GetLast(CancellationToken cancellationToken = default);
}
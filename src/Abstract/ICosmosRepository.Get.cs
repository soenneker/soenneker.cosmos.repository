using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    [Pure]
    ValueTask<bool> GetExists(string id);

    [Pure]
    ValueTask<bool> GetExistsByPartitionKey(string partitionKey);

    /// <summary>
    /// Get one item by Id (partition id and document id, or one guid if they're the same) <para/>
    /// Will not throw.
    /// </summary>
    /// <returns>null if cannot be found</returns>
    [Pure]
    ValueTask<TDocument?> GetItem(string id);

    /// <summary>
    /// Retrieves document(s) by partitionKey, and then executes .FirstOrDefault(). The assumption is there's only one document by the partition key specified. <para/>
    /// Will not throw.
    /// </summary>
    /// <param name="partitionKey"></param>
    /// <returns>null if cannot be found</returns>
    [Pure]
    ValueTask<TDocument?> GetItemByPartitionKey(string partitionKey);

    /// <summary>
    /// Will not throw.
    /// </summary>
    /// <returns>null if cannot be found</returns>
    [Pure]
    ValueTask<TDocument?> GetItem(string documentId, string partitionKey);

    /// <returns>
    /// The very first item ordered by createdAt ascending
    /// </returns>
    [Pure]
    ValueTask<TDocument?> GetFirst();

    /// <returns>
    /// The very first item ordered by createdAt descending
    /// </returns>
    [Pure]
    ValueTask<TDocument?> GetLast();
}
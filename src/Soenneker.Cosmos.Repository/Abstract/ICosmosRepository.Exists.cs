using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines exists operations for Cosmos DB documents.
/// </summary>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Checks whether the document addressed by a full ID exists.
    /// </summary>
    /// <param name="id">Identifier of the cosmos repository instance or registration to target.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the document exists; otherwise, <see langword="false"/>.</returns>
    [Pure]
    ValueTask<bool> Exists(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specified document exists in the given partition.
    /// </summary>
    /// <param name="documentId">Identifier of the target document.</param>
    /// <param name="partitionKey">Partition key used to route the database operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the document exists; otherwise, <see langword="false"/>.</returns>
    [Pure]
    ValueTask<bool> Exists(string documentId, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the query returns at least one document.
    /// </summary>
    /// <param name="query">The Cosmos LINQ query to evaluate.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the query returns a document; otherwise, <see langword="false"/>.</returns>
    [Pure]
    ValueTask<bool> Exists(IQueryable<TDocument> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the partition contains at least one document.
    /// </summary>
    /// <param name="partitionKey">Partition key used to route the database operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the partition contains a document; otherwise, <see langword="false"/>.</returns>
    [Pure]
    ValueTask<bool> ExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default);
}

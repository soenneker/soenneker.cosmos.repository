using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Checks for cosmos Repository.
    /// </summary>
    /// <param name="id">Identifier of the cosmos repository instance or registration to target.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if retrieves exists from the Cosmos Repository; otherwise, false.</returns>
    [Pure]
    ValueTask<bool> Exists(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for cosmos Repository.
    /// </summary>
    /// <param name="documentId">Identifier of the target document.</param>
    /// <param name="partitionKey">Partition key used to route the database operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if retrieves exists from the Cosmos Repository; otherwise, false.</returns>
    [Pure]
    ValueTask<bool> Exists(string documentId, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for cosmos Repository.
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if retrieves exists from the Cosmos Repository; otherwise, false.</returns>
    [Pure]
    ValueTask<bool> Exists(IQueryable<TDocument> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for by Partition Key.
    /// </summary>
    /// <param name="partitionKey">Partition key used to route the database operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>true if retrieves exists by partition key from the Cosmos Repository; otherwise, false.</returns>
    [Pure]
    ValueTask<bool> ExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default);
}

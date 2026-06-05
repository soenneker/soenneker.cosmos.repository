using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the cosmos repository contract.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<bool> Exists(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="documentId">The document id.</param>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<bool> Exists(string documentId, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<bool> Exists(IQueryable<TDocument> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the exists by partition key operation.
    /// </summary>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    [Pure]
    ValueTask<bool> ExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default);
}
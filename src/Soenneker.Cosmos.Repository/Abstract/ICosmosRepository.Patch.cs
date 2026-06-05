using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the cosmos repository contract.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
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
    /// Executes the patch item operation.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="operations">The operations.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    ValueTask<TDocument?> PatchItem(string id, List<PatchOperation> operations, bool useQueue = false, CancellationToken cancellationToken = default);
}
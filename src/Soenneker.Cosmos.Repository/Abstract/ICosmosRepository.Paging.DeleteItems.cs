using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Constants.Data;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the cosmos repository contract.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Deletes all paged.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes items paged.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="delayMs">The delay ms.</param>
    /// <param name="useQueue">The use queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteItemsPaged(QueryDefinition queryDefinition, int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default);
}
using Soenneker.Cosmos.Repository.Dtos;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Constants.Data;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines paging delete items operations for Cosmos DB documents.
/// </summary>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Deletes all paged.
    /// </summary>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="writeOptions">Can require ETags for this call. Null, empty, or false cannot disable the repository ETag requirement.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null, bool useQueue = false,
        CancellationToken cancellationToken = default, CosmosWriteOptions? writeOptions = null);

    /// <summary>
    /// Deletes all items page-by-page with bounded parallelism.
    /// </summary>
    /// <param name="maxConcurrency">The maximum number of concurrent delete operations.</param>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="writeOptions">Can require ETags for this call. Null, empty, or false cannot disable the repository ETag requirement.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteAllPagedParallel(int maxConcurrency, int pageSize = DataConstants.DefaultCosmosPageSize,
        CancellationToken cancellationToken = default, CosmosWriteOptions? writeOptions = null);

    /// <summary>
    /// Deletes items paged.
    /// </summary>
    /// <param name="queryDefinition">Database query and parameters to execute.</param>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="delayMs">Delay in milliseconds before the action runs.</param>
    /// <param name="useQueue">Whether to enqueue the write for background execution instead of awaiting Redis directly.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="writeOptions">Can require ETags for this call. Null, empty, or false cannot disable the repository ETag requirement.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteItemsPaged(QueryDefinition queryDefinition, int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null, bool useQueue = false,
        CancellationToken cancellationToken = default, CosmosWriteOptions? writeOptions = null);

    /// <summary>
    /// Deletes the queried items page-by-page with bounded parallelism.
    /// </summary>
    /// <param name="queryDefinition">Database query and parameters to execute.</param>
    /// <param name="maxConcurrency">The maximum number of concurrent delete operations.</param>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="writeOptions">Can require ETags for this call. Null, empty, or false cannot disable the repository ETag requirement.</param>
    /// <returns>A task that completes after the targeted files have been deleted.</returns>
    ValueTask DeleteItemsPagedParallel(QueryDefinition queryDefinition, int maxConcurrency, int pageSize = DataConstants.DefaultCosmosPageSize,
        CancellationToken cancellationToken = default, CosmosWriteOptions? writeOptions = null);
}

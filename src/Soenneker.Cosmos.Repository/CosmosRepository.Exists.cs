using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

/// <summary>
/// Represents the cosmos repository.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public ValueTask<bool> Exists(string id, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        return Exists(partitionKey, documentId, cancellationToken);
    }

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="documentId">The document id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<bool> Exists(string partitionKey, string documentId, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        using ResponseMessage resp = await container.ReadItemStreamAsync(
                                                        id: documentId, partitionKey: new PartitionKey(partitionKey), cancellationToken: cancellationToken)
                                                    .NoSync();

        return resp.StatusCode == HttpStatusCode.OK;
    }

    /// <summary>
    /// Executes the exists operation.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<bool> Exists(IQueryable<TDocument> query, CancellationToken cancellationToken = default)
    {
        using FeedIterator<TDocument> iterator = query.Take(1)
                                                      .ToFeedIterator();

        if (!iterator.HasMoreResults)
            return false;

        FeedResponse<TDocument> response = await iterator.ReadNextAsync(cancellationToken)
                                                         .NoSync();

        return response.Count > 0;
    }

    /// <summary>
    /// Executes the exists by partition key operation.
    /// </summary>
    /// <param name="partitionKey">The partition key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the result of the operation.</returns>
    public async ValueTask<bool> ExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        QueryDefinition q = new("SELECT VALUE 1 FROM c OFFSET 0 LIMIT 1");

        using FeedIterator it = container.GetItemQueryStreamIterator(q, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey),
            MaxItemCount = 1
        });

        if (!it.HasMoreResults)
            return false;

        using ResponseMessage resp = await it.ReadNextAsync(cancellationToken)
                                             .NoSync();

        return resp.StatusCode == HttpStatusCode.OK && resp.Content is not null && resp.Content.Length > 2;
    }
}
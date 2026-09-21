using Soenneker.Cosmos.Repository.Dtos;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Cosmos.Linq;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<bool> Exists(string id, CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        return Exists(documentId, partitionKey, readOptions, cancellationToken: cancellationToken);
    }

    public async ValueTask<bool> Exists(string documentId, string partitionKey,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        using ResponseMessage resp = await container.ReadItemStreamAsync(
                                                        id: documentId, partitionKey: new PartitionKey(partitionKey), requestOptions: (readOptions ?? DefaultReadOptions)?.ToItemRequestOptions(), cancellationToken: cancellationToken)
                                                    .NoSync();

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return false;

        resp.EnsureSuccessStatusCode();
        return true;
    }

    public async ValueTask<bool> Exists(IQueryable<TDocument> query, CancellationToken cancellationToken = default)
    {
        query = query.WithNullSemantics();

        using FeedIterator<int> iterator = query.Select(static _ => 1).Take(1)
                                                      .ToFeedIterator();

        return await HasAnyResults(iterator, cancellationToken).NoSync();
    }

    private static async ValueTask<bool> HasAnyResults<T>(FeedIterator<T> iterator, CancellationToken cancellationToken)
    {
        while (iterator.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken).NoSync();
            if (response.Count > 0)
                return true;
        }

        return false;
    }

    public async ValueTask<bool> ExistsByPartitionKey(string partitionKey, CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        QueryDefinition q = new("SELECT VALUE 1 FROM c OFFSET 0 LIMIT 1");

        QueryRequestOptions requestOptions = (readOptions ?? DefaultReadOptions)?.ToQueryRequestOptions() ?? new QueryRequestOptions();
        requestOptions.PartitionKey = new PartitionKey(partitionKey);
        requestOptions.MaxItemCount = 1;
        requestOptions.EnableOptimisticDirectExecution = true;

        using FeedIterator<int> it = container.GetItemQueryIterator<int>(q, requestOptions: requestOptions);

        return await HasAnyResults(it, cancellationToken).NoSync();
    }
}

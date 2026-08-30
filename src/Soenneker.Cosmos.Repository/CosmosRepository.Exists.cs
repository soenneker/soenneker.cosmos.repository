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

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<bool> Exists(string id, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        return Exists(documentId, partitionKey, cancellationToken);
    }

    public async ValueTask<bool> Exists(string documentId, string partitionKey, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        using ResponseMessage resp = await container.ReadItemStreamAsync(
                                                        id: documentId, partitionKey: new PartitionKey(partitionKey), cancellationToken: cancellationToken)
                                                    .NoSync();

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return false;

        resp.EnsureSuccessStatusCode();
        return true;
    }

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

    public async ValueTask<bool> ExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        QueryDefinition q = new("SELECT VALUE 1 FROM c OFFSET 0 LIMIT 1");

        using FeedIterator<int> it = container.GetItemQueryIterator<int>(q, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey),
            MaxItemCount = 1,
            EnableOptimisticDirectExecution = true
        });

        if (!it.HasMoreResults)
            return false;

        FeedResponse<int> response = await it.ReadNextAsync(cancellationToken)
                                             .NoSync();

        return response.Count > 0;
    }
}

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

        return Exists(partitionKey, documentId, cancellationToken);
    }

    public async ValueTask<bool> Exists(string partitionKey, string documentId, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        using ResponseMessage resp = await container.ReadItemStreamAsync(
                                                        id: documentId, partitionKey: new PartitionKey(partitionKey), cancellationToken: cancellationToken)
                                                    .NoSync();

        // ReadItemStreamAsync doesn't throw on 404; just check the status.
        return resp.StatusCode == HttpStatusCode.OK;
    }

    public async ValueTask<bool> Exists(IQueryable<TDocument> query, CancellationToken cancellationToken = default)
    {
        using FeedIterator<int> iterator = query.Select(_ => 1).Take(1).ToFeedIterator();

        if (!iterator.HasMoreResults)
            return false;

        FeedResponse<int> response = await iterator.ReadNextAsync(cancellationToken).NoSync();

        return response.Count > 0;
    }

    public async ValueTask<bool> ExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        var q = new QueryDefinition("SELECT VALUE 1 FROM c OFFSET 0 LIMIT 1");

        using FeedIterator? it = container.GetItemQueryStreamIterator(q, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey),
            MaxItemCount = 1
        });

        if (!it.HasMoreResults)
            return false;

        using ResponseMessage? resp = await it.ReadNextAsync(cancellationToken).NoSync();

        // 200 with an empty array means "no items"
        // Payload is a minimal JSON array like [1]; we don't parse it.
        return resp.StatusCode == HttpStatusCode.OK && resp.Content.Length > 2; // >2 to exclude "[]"
    }
}
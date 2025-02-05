using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public async ValueTask<bool> Exists(string id, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        TDocument? doc = await GetItem(documentId, partitionKey, cancellationToken).NoSync();

        return doc != null;
    }

    public async ValueTask<bool> Exists(IQueryable<TDocument> query, CancellationToken cancellationToken = default)
    {
        int count = await query
            .Select(c => c.Id)
            .Take(1)
            .CountAsync(cancellationToken)
            .NoSync();

        return count > 0;
    }

    public async ValueTask<bool> ExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(CosmosRequestOptions.MaxItemCountOne, cancellationToken).NoSync();

        query = query.Where(c => c.PartitionKey == partitionKey);

        return await Exists(query, cancellationToken).NoSync();
    }
}
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
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
        IQueryable<string> newQuery = query.Select(c => c.Id);
        newQuery = newQuery.Take(1);

        string? id = await GetItem(newQuery, cancellationToken).NoSync();

        return id != null;
    }

    public async ValueTask<bool> ExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(null, cancellationToken).NoSync();

        query = query.Where(c => c.PartitionKey == partitionKey);

        return await Exists(query, cancellationToken).NoSync();
    }
}
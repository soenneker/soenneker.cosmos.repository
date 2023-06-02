using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Extensions.String;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public async ValueTask<bool> GetExists(string id)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        TDocument? doc = await GetItem(documentId, partitionKey).ConfigureAwait(false);

        bool result = doc != null;
        return result;
    }

    public async ValueTask<bool> GetExistsByPartitionKey(string partitionKey)
    {
        IQueryable<TDocument> query = await BuildQueryable().ConfigureAwait(false);

        query = query.Where(c => c.PartitionKey == partitionKey);
        IQueryable<string> newQuery = query.Select(c => c.Id);

        string? docId = await GetItem(newQuery).ConfigureAwait(false);

        bool result = docId != null;
        return result;
    }

    public virtual ValueTask<TDocument?> GetItem(string id)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        return GetItem(documentId, partitionKey);
    }

    public async ValueTask<TDocument?> GetItemByPartitionKey(string partitionKey)
    {
        IQueryable<TDocument> query = await BuildQueryable().ConfigureAwait(false);
        query = query.Where(c => c.PartitionKey == partitionKey);

        TDocument? doc = await GetItem(query).ConfigureAwait(false);

        return doc;
    }
    
    public async ValueTask<TDocument?> GetItem(string documentId, string partitionKey)
    {
        try
        {
            if (_log)
            {
                string logId = documentId == partitionKey ? documentId : $"{partitionKey}:{documentId}";

                Logger.LogDebug("-- COSMOS: {method} ({type}): {id}", MethodUtil.Get(), typeof(TDocument).Name, logId);
            }

            CancellationToken cancellationToken = _cancellationUtil.Get();

            Microsoft.Azure.Cosmos.Container container = await Container.ConfigureAwait(false);

            ItemResponse<TDocument> response = await container.ReadItemAsync<TDocument>(documentId, new PartitionKey(partitionKey),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            TDocument? doc = response.Resource;
            return doc;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async ValueTask<TDocument?> GetFirst()
    {
        IQueryable<TDocument> query = await BuildQueryable().ConfigureAwait(false);
        query = query.OrderBy(x => x.CreatedAt);
        query = query.Take(1);

        TDocument? doc = await GetItem(query).ConfigureAwait(false);

        return doc;
    }

    public async ValueTask<TDocument?> GetLast()
    {
        IQueryable<TDocument> query = await BuildQueryable().ConfigureAwait(false);
        query = query.OrderByDescending(x => x.CreatedAt);
        query = query.Take(1);

        TDocument? doc = await GetItem(query).ConfigureAwait(false);

        return doc;
    }
}
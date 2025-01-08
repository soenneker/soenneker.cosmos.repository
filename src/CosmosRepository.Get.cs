using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Dtos.IdNamePair;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public async ValueTask<bool> GetExists(string id, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        TDocument? doc = await GetItem(documentId, partitionKey, cancellationToken).NoSync();

        bool result = doc != null;
        return result;
    }

    public async ValueTask<bool> GetExistsByPartitionKey(string partitionKey, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();

        query = query.Where(c => c.PartitionKey == partitionKey);
        IQueryable<string> newQuery = query.Select(c => c.Id);

        string? docId = await GetItem(newQuery, cancellationToken).NoSync();

        bool result = docId != null;
        return result;
    }

    public virtual ValueTask<TDocument?> GetItem(string id, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        return GetItem(documentId, partitionKey, cancellationToken);
    }

    public async ValueTask<TDocument?> GetItemByPartitionKey(string partitionKey, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.Where(c => c.PartitionKey == partitionKey);

        return await GetItem(query, cancellationToken).NoSync();
    }

    public ValueTask<TDocument?> GetItemByIdNamePair(IdNamePair idNamePair, CancellationToken cancellationToken = default)
    {
        return GetItem(idNamePair.Id, idNamePair.Id, cancellationToken);
    }

    public async ValueTask<TDocument?> GetItem(string documentId, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_log)
            {
                string logId = documentId == partitionKey ? documentId : $"{partitionKey}:{documentId}";

                Logger.LogDebug("-- COSMOS: {method} ({type}): {id}", MethodUtil.Get(), typeof(TDocument).Name, logId);
            }

            Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

            ItemResponse<TDocument> response = await container.ReadItemAsync<TDocument>(documentId, new PartitionKey(partitionKey),
                cancellationToken: cancellationToken).NoSync();

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async ValueTask<TDocument?> GetFirst(CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.OrderBy(x => x.CreatedAt);
        query = query.Take(1);

        return await GetItem(query, cancellationToken).NoSync();
    }

    public virtual async ValueTask<TDocument?> GetLast(CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();
        query = query.OrderByDescending(x => x.CreatedAt);
        query = query.Take(1);

        return await GetItem(query, cancellationToken).NoSync();
    }
}
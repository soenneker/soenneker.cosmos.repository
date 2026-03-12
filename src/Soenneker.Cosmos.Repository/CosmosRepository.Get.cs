using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Documents.Document;
using Soenneker.Dtos.IdNamePair;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual ValueTask<TDocument?> GetItem(string id, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        return GetItem(documentId, partitionKey, cancellationToken);
    }

    public async ValueTask<TDocument?> GetItemByPartitionKey(string partitionKey, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        var q = new QueryDefinition("SELECT TOP 1 * FROM c ORDER BY c._ts DESC");

        using FeedIterator<TDocument>? it = container.GetItemQueryIterator<TDocument>(q, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey),
            MaxItemCount = 1
        });

        FeedResponse<TDocument>? page = await it.ReadNextAsync(cancellationToken).NoSync();

        if (page.Count == 0) 
            return null;

        using IEnumerator<TDocument> e = page.Resource.GetEnumerator();
        return e.MoveNext() ? e.Current : null;
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
                                                                  cancellationToken: cancellationToken)
                                                              .NoSync();

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async ValueTask<TDocument?> GetFirst(CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(CosmosRequestOptions.MaxItemCountOne, cancellationToken).NoSync();

        query = query.OrderBy(x => x.CreatedAt);

        return await GetItem(query, cancellationToken).NoSync();
    }

    public virtual async ValueTask<TDocument?> GetLast(CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(CosmosRequestOptions.MaxItemCountOne, cancellationToken).NoSync();

        query = query.OrderByDescending(x => x.CreatedAt);

        return await GetItem(query, cancellationToken).NoSync();
    }
}
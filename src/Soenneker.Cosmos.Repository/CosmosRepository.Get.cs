using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Cosmos.Repository.Dtos;
using Soenneker.Documents.Document;
using Soenneker.Dtos.IdNamePair;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<CosmosItem<TDocument>?> GetItemWithETag(string id, CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = id.ToSplitId();
        return GetItemWithETag(documentId, partitionKey, readOptions, cancellationToken: cancellationToken);
    }

    public ValueTask<CosmosItem<TDocument>?> GetItemWithETag(string documentId, string partitionKey,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        return GetItemWithETagCore(documentId, new PartitionKey(partitionKey), (readOptions ?? DefaultReadOptions)?.ToItemRequestOptions(), cancellationToken);
    }

    private async ValueTask<CosmosItem<TDocument>?> GetItemWithETagCore(string documentId, PartitionKey partitionKey,
        ItemRequestOptions? requestOptions, CancellationToken cancellationToken)
    {
        try
        {
            Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
            ItemResponse<TDocument> response = await container.ReadItemAsync<TDocument>(documentId, partitionKey,
                    requestOptions: requestOptions, cancellationToken: cancellationToken)
                .NoSync();

            return new CosmosItem<TDocument>(response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual ValueTask<TDocument?> GetItem(string id, CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        (string partitionKey, string documentId) = id.ToSplitId();

        return GetItem(documentId, partitionKey, readOptions, cancellationToken: cancellationToken);
    }

    public async ValueTask<TDocument?> GetItemByPartitionKey(string partitionKey,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        var q = new QueryDefinition("SELECT TOP 1 * FROM c");

        QueryRequestOptions requestOptions = (readOptions ?? DefaultReadOptions)?.ToQueryRequestOptions() ?? new QueryRequestOptions();
        requestOptions.PartitionKey = new PartitionKey(partitionKey);
        requestOptions.MaxItemCount = 1;
        requestOptions.EnableOptimisticDirectExecution = true;

        using FeedIterator<TDocument> it = container.GetItemQueryIterator<TDocument>(q, requestOptions: requestOptions);

        return await ReadFirst(it, cancellationToken).NoSync();
    }

    public async ValueTask<TDocument?> GetLatestByPartitionKey(string partitionKey,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        var q = new QueryDefinition("SELECT TOP 1 * FROM c ORDER BY c.createdAt DESC");

        QueryRequestOptions requestOptions = (readOptions ?? DefaultReadOptions)?.ToQueryRequestOptions() ?? new QueryRequestOptions();
        requestOptions.PartitionKey = new PartitionKey(partitionKey);
        requestOptions.MaxItemCount = 1;
        requestOptions.EnableOptimisticDirectExecution = true;

        using FeedIterator<TDocument> it = container.GetItemQueryIterator<TDocument>(q, requestOptions: requestOptions);

        return await ReadFirst(it, cancellationToken).NoSync();
    }

    public ValueTask<TDocument?> GetItemByIdNamePair(IdNamePair idNamePair,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        return GetItem(idNamePair.Id, idNamePair.Id, readOptions, cancellationToken: cancellationToken);
    }

    public async ValueTask<TDocument?> GetItem(string documentId, string partitionKey,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_log && Logger.IsEnabled(LogLevel.Debug))
            {
                string logId = documentId == partitionKey
                    ? documentId
                    : string.Concat(partitionKey, ":", documentId);

                Logger.LogDebug("-- COSMOS: {method} ({type}): {id}", MethodUtil.Get(), typeof(TDocument).Name, logId);
            }

            Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

            ItemResponse<TDocument> response = await container.ReadItemAsync<TDocument>(
                documentId,
                new PartitionKey(partitionKey),
                requestOptions: (readOptions ?? DefaultReadOptions)?.ToItemRequestOptions(), cancellationToken: cancellationToken).NoSync();

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async ValueTask<TDocument?> GetFirst(CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(GetSingleItemQueryOptions(readOptions), cancellationToken: cancellationToken).NoSync();

        query = query.OrderBy(static x => x.CreatedAt);

        return await GetItem(query, cancellationToken: cancellationToken).NoSync();
    }

    public virtual async ValueTask<TDocument?> GetLast(CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(GetSingleItemQueryOptions(readOptions), cancellationToken: cancellationToken).NoSync();

        query = query.OrderByDescending(static x => x.CreatedAt);

        return await GetItem(query, cancellationToken: cancellationToken).NoSync();
    }

    private QueryRequestOptions GetSingleItemQueryOptions(CosmosReadOptions? readOptions)
    {
        QueryRequestOptions options = (readOptions ?? DefaultReadOptions)?.ToQueryRequestOptions() ?? CosmosRequestOptions.MaxItemCountOne;
        options.MaxItemCount = 1;
        return options;
    }
}

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Documents.Document;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

//references to documentation about cosmos linq to sql and cosmos linq query 
//https://docs.microsoft.com/en-us/azure/cosmos-db/sql/sql-query-linq-to-sql
//https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.container.getitemlinqquery?view=azure-dotnet

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<IQueryable<TDocument>> BuildQueryable(QueryRequestOptions? queryRequestOptions = null, CancellationToken cancellationToken = default)
    {
        return BuildQueryable<TDocument>(queryRequestOptions, cancellationToken);
    }

    public async ValueTask<IQueryable<T>> BuildQueryable<T>(QueryRequestOptions? queryRequestOptions = null, CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        return container.GetItemLinqQueryable<T>(requestOptions: queryRequestOptions);
    }

    public ValueTask<IQueryable<TDocument>> BuildPagedQueryable(int pageSize = 500, string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        return BuildPagedQueryable<TDocument>(pageSize, continuationToken, cancellationToken);
    }

    public async ValueTask<IQueryable<T>> BuildPagedQueryable<T>(int pageSize = 500, string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        var requestOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize
        };

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        return container.GetItemLinqQueryable<T>(continuationToken: continuationToken, requestOptions: requestOptions);
    }

    public async ValueTask<int> Count(CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(null, cancellationToken).NoSync();

        return await Count(query, cancellationToken).NoSync();
    }

    public async ValueTask<int> Count(IQueryable<TDocument> query, CancellationToken cancellationToken = default)
    {
        Response<int>? response = await query.CountAsync(cancellationToken: cancellationToken).NoSync();

        return response.Resource;
    }

    public async ValueTask<bool> Any(CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(null, cancellationToken).NoSync();

        return await Exists(query, cancellationToken).NoSync();
    }

    public async ValueTask<bool> None(CancellationToken cancellationToken = default)
    {
        return !await Any(cancellationToken).NoSync();
    }

    public async ValueTask<T?> GetItem<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        LogQuery<T>(query, MethodUtil.Get());

        using FeedIterator<T> iterator = query.Take(1).ToFeedIterator();

        FeedResponse<T>? page = await iterator.ReadNextAsync(cancellationToken).NoSync();

        if (page.Count == 0)
            return default;

        if (page.Resource is IReadOnlyList<T> {Count: > 0} roList)
            return roList[0];

        using IEnumerator<T> e = page.Resource.GetEnumerator();
        return e.MoveNext() ? e.Current : default;
    }

    public async ValueTask<List<T>> GetItems<T>(IQueryable<T> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        LogQuery<T>(query, MethodUtil.Get());

        using FeedIterator<T>? iterator = query.ToFeedIterator();
        return await DrainIterator(iterator, delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null, cancellationToken).NoSync();
    }

    public async ValueTask<List<TDocument>> GetItems(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        LogQuery<TDocument>(query, MethodUtil.Get());

        using FeedIterator<TDocument>? iterator = query.ToFeedIterator();
        return await DrainIterator(iterator, delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null, cancellationToken).NoSync();
    }
}
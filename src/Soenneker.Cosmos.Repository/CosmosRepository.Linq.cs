using Soenneker.Cosmos.Repository.Dtos;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Cosmos.Linq;
using Soenneker.Documents.Document;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

//references to documentation about cosmos linq to sql and cosmos linq query 
//https://docs.microsoft.com/en-us/azure/cosmos-db/sql/sql-query-linq-to-sql
//https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.container.getitemlinqquery?view=azure-dotnet

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IQueryable<TDocument>> BuildQueryable(QueryRequestOptions? queryRequestOptions = null,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        return BuildQueryable<TDocument>(queryRequestOptions, readOptions, cancellationToken: cancellationToken);
    }

    public ValueTask<IQueryable<T>> BuildQueryable<T>(QueryRequestOptions? queryRequestOptions = null,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        return BuildQueryableCore<T>(queryRequestOptions ?? (readOptions ?? DefaultReadOptions)?.ToQueryRequestOptions(), cancellationToken);
    }

    private async ValueTask<IQueryable<T>> BuildQueryableCore<T>(QueryRequestOptions? queryRequestOptions, CancellationToken cancellationToken)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();
        return container.GetItemLinqQueryable<T>(requestOptions: queryRequestOptions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IQueryable<TDocument>> BuildPagedQueryable(int pageSize = 500, string? continuationToken = null,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        return BuildPagedQueryable<TDocument>(pageSize, continuationToken, readOptions, cancellationToken: cancellationToken);
    }

    public ValueTask<IQueryable<T>> BuildPagedQueryable<T>(int pageSize = 500, string? continuationToken = null,
        CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        QueryRequestOptions requestOptions = (readOptions ?? DefaultReadOptions)?.ToQueryRequestOptions() ?? new QueryRequestOptions();
        requestOptions.MaxItemCount = pageSize;

        return BuildPagedQueryableCore(this, requestOptions, continuationToken, cancellationToken);

        static async ValueTask<IQueryable<T>> BuildPagedQueryableCore(CosmosRepository<TDocument> repo, QueryRequestOptions requestOptions,
            string? continuationToken, CancellationToken cancellationToken)
        {
            Microsoft.Azure.Cosmos.Container container = await repo.Container(cancellationToken)
                                                                   .NoSync();
            return container.GetItemLinqQueryable<T>(continuationToken: continuationToken, requestOptions: requestOptions);
        }
    }

    public async ValueTask<int> Count(CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryableCore<TDocument>((readOptions ?? DefaultReadOptions)?.ToQueryRequestOptions(), cancellationToken)
            .NoSync();

        return await Count(query, cancellationToken: cancellationToken)
            .NoSync();
    }

    public async ValueTask<int> Count(IQueryable<TDocument> query, CancellationToken cancellationToken = default)
    {
        query = query.WithNullSemantics();

        Response<int> response = await query.CountAsync(cancellationToken: cancellationToken)
                                            .NoSync();

        return response.Resource;
    }

    public async ValueTask<bool> Any(CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryableCore<TDocument>((readOptions ?? DefaultReadOptions)?.ToQueryRequestOptions(), cancellationToken)
            .NoSync();

        return await Exists(query, cancellationToken: cancellationToken)
            .NoSync();
    }

    public async ValueTask<bool> None(CosmosReadOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        return !await Any(readOptions, cancellationToken: cancellationToken)
            .NoSync();
    }

    public async ValueTask<T?> GetItem<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        query = query.WithNullSemantics();

        LogQuery<T>(query, MethodUtil.Get());

        using FeedIterator<T> iterator = query.Take(1)
                                              .ToFeedIterator();

        return await ReadFirst(iterator, cancellationToken).NoSync();
    }

    private static async ValueTask<T?> ReadFirst<T>(FeedIterator<T> iterator, CancellationToken cancellationToken)
    {
        while (iterator.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FeedResponse<T> page = await iterator.ReadNextAsync(cancellationToken).NoSync();
            if (page.Count == 0)
                continue;

            if (page.Resource is IReadOnlyList<T> list)
                return list[0];

            using IEnumerator<T> enumerator = page.Resource.GetEnumerator();
            if (enumerator.MoveNext())
                return enumerator.Current;
        }

        return default;
    }

    public async ValueTask<List<T>> GetItems<T>(IQueryable<T> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        query = query.WithNullSemantics();

        LogQuery<T>(query, MethodUtil.Get());

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        using FeedIterator<T> iterator = query.ToFeedIterator();

        return await DrainIterator(iterator, delay, cancellationToken)
            .NoSync();
    }

    public async ValueTask<List<TDocument>> GetItems(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        query = query.WithNullSemantics();

        LogQuery<TDocument>(query, MethodUtil.Get());

        using FeedIterator<TDocument>? iterator = query.ToFeedIterator();
        return await DrainIterator(iterator, delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null, cancellationToken)
            .NoSync();
    }
}

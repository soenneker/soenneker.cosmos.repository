using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Documents.Document;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

//references to documentation about cosmos linq to sql and cosmos linq queryable 
//https://docs.microsoft.com/en-us/azure/cosmos-db/sql/sql-query-linq-to-sql
//https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.container.getitemlinqqueryable?view=azure-dotnet

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<IQueryable<TDocument>> BuildQueryable(CancellationToken cancellationToken = default)
    {
        return BuildQueryable<TDocument>(cancellationToken);
    }

    public async ValueTask<IQueryable<T>> BuildQueryable<T>(CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        IOrderedQueryable<T> result = container.GetItemLinqQueryable<T>();
        return result;
    }

    public ValueTask<IQueryable<TDocument>> BuildPagedQueryable(int pageSize = 500, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        return BuildPagedQueryable<TDocument>(pageSize, continuationToken, cancellationToken);
    }

    public async ValueTask<IQueryable<T>> BuildPagedQueryable<T>(int pageSize = 500, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        QueryRequestOptions requestOptions = new()
        {
            MaxItemCount = pageSize
        };

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        IOrderedQueryable<T> queryable = container.GetItemLinqQueryable<T>(continuationToken: continuationToken, requestOptions: requestOptions);
        
        return queryable;
    }

    public async ValueTask<int> Count(CancellationToken cancellationToken = default)
    {
        IQueryable<TDocument> query = await BuildQueryable(cancellationToken).NoSync();

        int result = await Count(query, cancellationToken).NoSync();
        return result;
    }

    public async ValueTask<int> Count(IQueryable<TDocument> query, CancellationToken cancellationToken = default)
    {
        Response<int>? response = await query.CountAsync(cancellationToken: cancellationToken).NoSync();

        int result = response.Resource;
        return result;
    }

    public async ValueTask<bool> Any(CancellationToken cancellationToken = default)
    {
        // TODO: there probably is a better way to do this than counting
        int count = await Count(cancellationToken).NoSync();
        return count != 0;
    }

    public async ValueTask<bool> None(CancellationToken cancellationToken = default)
    {
        bool result = !await Any(cancellationToken).NoSync();
        return result;
    }

    public async ValueTask<T?> GetItem<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        List<T> items = await GetItems(queryable, cancellationToken: cancellationToken).NoSync();

        T? result = items.FirstOrDefault();
        return result;
    }

    public async ValueTask<List<T>> GetItems<T>(IQueryable<T> queryable, double? delayMs = null, CancellationToken cancellationToken = default)
    {
        LogQuery<T>(queryable, MethodUtil.Get());

        TimeSpan? timeSpanDelay = null;

        if (delayMs != null)
            timeSpanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

        using FeedIterator<T> iterator = queryable.ToFeedIterator();

        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken).NoSync();

            results.AddRange(response.ToList());

            if (delayMs != null)
                await Task.Delay(timeSpanDelay!.Value, cancellationToken: cancellationToken).NoSync();
        }

        return results;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Soenneker.Documents.Document;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

//references to documentation about cosmos linq to sql and cosmos linq queryable 
//https://docs.microsoft.com/en-us/azure/cosmos-db/sql/sql-query-linq-to-sql
//https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.cosmos.container.getitemlinqqueryable?view=azure-dotnet

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<IQueryable<TDocument>> BuildQueryable()
    {
        return BuildQueryable<TDocument>();
    }

    public async ValueTask<IQueryable<T>> BuildQueryable<T>()
    {
        Microsoft.Azure.Cosmos.Container container = await Container;
        IOrderedQueryable<T> result = container.GetItemLinqQueryable<T>();
        return result;
    }

    public ValueTask<IQueryable<TDocument>> BuildPagedQueryable(int pageSize = 500, string? continuationToken = null)
    {
        return BuildPagedQueryable<TDocument>(pageSize, continuationToken);
    }

    public async ValueTask<IQueryable<T>> BuildPagedQueryable<T>(int pageSize = 500, string? continuationToken = null)
    {
        QueryRequestOptions requestOptions = new()
        {
            MaxItemCount = pageSize
        };

        Microsoft.Azure.Cosmos.Container container = await Container;

        IOrderedQueryable<T> queryable = container.GetItemLinqQueryable<T>(continuationToken: continuationToken, requestOptions: requestOptions);
        
        return queryable;
    }

    public async ValueTask<int> Count()
    {
        IQueryable<TDocument> query = await BuildQueryable();

        int result = await Count(query);
        return result;
    }

    public async ValueTask<int> Count(IQueryable<TDocument> query)
    {
        CancellationToken cancellationToken = _cancellationUtil.Get();

        Response<int>? response = await query.CountAsync(cancellationToken: cancellationToken);

        int result = response.Resource;
        return result;
    }

    public async ValueTask<bool> Any()
    {
        // TODO: there probably is a better way to do this than counting
        int count = await Count();
        return count != 0;
    }

    public async ValueTask<bool> None()
    {
        bool result = !await Any();
        return result;
    }

    public async ValueTask<T?> GetItem<T>(IQueryable<T> queryable)
    {
        List<T> items = await GetItems(queryable);

        T? result = items.FirstOrDefault();
        return result;
    }

    public async ValueTask<List<T>> GetItems<T>(IQueryable<T> queryable, double? delayMs = null)
    {
        LogQuery<T>(queryable, MethodUtil.Get());

        TimeSpan? timeSpanDelay = null;

        if (delayMs != null)
            timeSpanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

        CancellationToken cancellationToken = _cancellationUtil.Get();

        using FeedIterator<T> iterator = queryable.ToFeedIterator();

        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken);

            results.AddRange(response.ToList());

            if (delayMs != null)
                await Task.Delay(timeSpanDelay!.Value, cancellationToken: cancellationToken);
        }

        return results;
    }
}
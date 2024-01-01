using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Documents.Document;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask ExecuteOnGetItemsPaged(IQueryable<TDocument> queryable, Func<List<TDocument>, ValueTask> resultTask)
    {
        return ExecuteOnGetItemsPaged<TDocument>(queryable, resultTask);
    }

    public async ValueTask ExecuteOnGetItemsPaged<T>(IQueryable<T> queryable, Func<List<T>, ValueTask> resultTask)
    {
        string? continuationToken;

        do
        {
            (List<T>? docs, string? newContinuationToken) = await GetItemsPaged(queryable).NoSync();

            continuationToken = newContinuationToken;

            await resultTask(docs).NoSync();
        } while (continuationToken != null);
    }

    public async ValueTask ExecuteOnGetItemsPaged(QueryDefinition queryDefinition, int pageSize, Func<List<TDocument>, ValueTask> resultTask)
    {
        string? continuationToken = null;

        do
        {
            (List<TDocument>? docs, string? newContinuationToken) = await GetItemsPaged(queryDefinition, pageSize, continuationToken).NoSync();

            continuationToken = newContinuationToken;

            await resultTask(docs).NoSync();
        } while (continuationToken != null);
    }

    public async ValueTask ExecuteOnGetAllPaged(int pageSize, Func<List<TDocument>, ValueTask> resultTask)
    {
        string? continuationToken = null;

        do
        {
            (List<TDocument>? docs, string? newContinuationToken) = await GetAllPaged(pageSize, continuationToken).NoSync();

            continuationToken = newContinuationToken;

            await resultTask(docs).NoSync();
        } while (continuationToken != null);
    }
}
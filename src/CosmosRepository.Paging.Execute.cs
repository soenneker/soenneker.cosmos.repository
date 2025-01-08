using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Documents.Document;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual ValueTask ExecuteOnGetItemsPaged(IQueryable<TDocument> query, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        return ExecuteOnGetItemsPaged<TDocument>(query, resultTask, cancellationToken);
    }

    public virtual async ValueTask ExecuteOnGetItemsPaged<T>(IQueryable<T> query, Func<List<T>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        string? continuationToken;

        do
        {
            (List<T>? docs, string? newContinuationToken) = await GetItemsPaged(query, cancellationToken).NoSync();

            continuationToken = newContinuationToken;

            await resultTask(docs).NoSync();
        } while (continuationToken != null);
    }

    public async ValueTask ExecuteOnGetItemsPaged(QueryDefinition queryDefinition, int pageSize, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        string? continuationToken = null;

        do
        {
            (List<TDocument>? docs, string? newContinuationToken) = await GetItemsPaged(queryDefinition, pageSize, continuationToken, cancellationToken).NoSync();

            continuationToken = newContinuationToken;

            await resultTask(docs).NoSync();
        } while (continuationToken != null);
    }

    public virtual async ValueTask ExecuteOnGetAllPaged(int pageSize, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        string? continuationToken = null;

        do
        {
            (List<TDocument>? docs, string? newContinuationToken) = await GetAllPaged(pageSize, continuationToken, cancellationToken).NoSync();

            continuationToken = newContinuationToken;

            await resultTask(docs).NoSync();
        } while (continuationToken != null);
    }
}
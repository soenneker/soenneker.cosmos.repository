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
namespace Soenneker.Cosmos.Repository;
public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual ValueTask ExecuteOnGetItemsPaged(IQueryable<TDocument> query, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        return ExecuteOnGetItemsPaged<TDocument>(query, resultTask, cancellationToken);
    }

    public virtual async ValueTask ExecuteOnGetItemsPaged<T>(IQueryable<T> query, Func<List<T>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        using FeedIterator<T> iterator = query.ToFeedIterator();
        await ExecuteOnFeedIterator(iterator, resultTask, cancellationToken).NoSync();
    }

    /// <summary>
    /// Executes on Feed Iterator.
    /// </summary>
    /// <typeparam name="T">Type of value handled by the Cosmos Repository.</typeparam>
    /// <param name="iterator">Iterator for the execute on feed iterator operation.</param>
    /// <param name="resultTask">Callback used by execute on feed iterator.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the execute on feed iterator operation is complete.</returns>
    public static async ValueTask ExecuteOnFeedIterator<T>(FeedIterator<T> iterator, Func<List<T>, ValueTask> resultTask,
        CancellationToken cancellationToken = default)
    {
        while (iterator.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken).NoSync();
            List<T> docs;

            if (response.Resource is List<T> list)
            {
                docs = list;
            }
            else
            {
                docs = new List<T>(response.Count);

                foreach (T item in response)
                {
                    docs.Add(item);
                }
            }

            await resultTask(docs).NoSync();
        }
    }

    public async ValueTask ExecuteOnGetItemsPaged(QueryDefinition queryDefinition, int pageSize, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        string? continuationToken = null;

        do
        {
            (List<TDocument> docs, string? newContinuationToken) = await GetItemsPaged(queryDefinition, pageSize, continuationToken, cancellationToken).NoSync();

            continuationToken = newContinuationToken;

            await resultTask(docs).NoSync();
        } while (continuationToken != null);
    }

    public virtual async ValueTask ExecuteOnGetAllPaged(int pageSize, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        string? continuationToken = null;

        do
        {
            (List<TDocument> docs, string? newContinuationToken) = await GetAllPaged(pageSize, continuationToken, cancellationToken).NoSync();

            continuationToken = newContinuationToken;

            await resultTask(docs).NoSync();
        } while (continuationToken != null);
    }
}

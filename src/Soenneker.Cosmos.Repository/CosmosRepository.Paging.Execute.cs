using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Documents.Document;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Cosmos.Repository;

/// <summary>
/// Represents the cosmos repository.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    /// <summary>
    /// Executes the execute on get items paged operation.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="resultTask">The result task.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual ValueTask ExecuteOnGetItemsPaged(IQueryable<TDocument> query, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        return ExecuteOnGetItemsPaged<TDocument>(query, resultTask, cancellationToken);
    }

    /// <summary>
    /// Executes the execute on get items paged operation.
    /// </summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="resultTask">The result task.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async ValueTask ExecuteOnGetItemsPaged<T>(IQueryable<T> query, Func<List<T>, ValueTask> resultTask, CancellationToken cancellationToken = default)
    {
        string? continuationToken;

        do
        {
            (List<T> docs, string? newContinuationToken) = await GetItemsPaged(query, cancellationToken).NoSync();

            continuationToken = newContinuationToken;

            await resultTask(docs).NoSync();
        } while (continuationToken != null);
    }

    /// <summary>
    /// Executes the execute on get items paged operation.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="resultTask">The result task.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Executes the execute on get all paged operation.
    /// </summary>
    /// <param name="pageSize">The page size.</param>
    /// <param name="resultTask">The result task.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Be sure to pass a query that was built via <see cref="BuildPagedQueryable"/>
    /// </summary>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="resultTask">Callback used by execute on get items paged.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the execute on get items paged operation is complete.</returns>
    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    ValueTask ExecuteOnGetItemsPaged(IQueryable<TDocument> query, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default);

    /// <summary>
    /// Be sure to pass a query that was built via <see cref="BuildPagedQueryable"/>
    /// </summary>
    /// <typeparam name="T">Type of value handled by the cosmos repository.</typeparam>
    /// <param name="query">CSS media-query expression to evaluate against the current viewport.</param>
    /// <param name="resultTask">Callback used by execute on get items paged.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the execute on get items paged operation is complete.</returns>
    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    ValueTask ExecuteOnGetItemsPaged<T>(IQueryable<T> query, Func<List<T>, ValueTask> resultTask, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wraps <see cref="GetAllPaged"/> and hides away the continuationToken logic in a do-while.
    /// </summary>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="resultTask">Callback used by execute on get all paged.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the execute on get all paged operation is complete.</returns>
    ValueTask ExecuteOnGetAllPaged(int pageSize, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes on Get Items Paged.
    /// </summary>
    /// <param name="queryDefinition">Database query and parameters to execute.</param>
    /// <param name="pageSize">Maximum number of items to request per page.</param>
    /// <param name="resultTask">Callback used by execute on get items paged.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the execute on get items paged operation is complete.</returns>
    ValueTask ExecuteOnGetItemsPaged(QueryDefinition queryDefinition, int pageSize, Func<List<TDocument>, ValueTask> resultTask, CancellationToken cancellationToken = default);
    
}

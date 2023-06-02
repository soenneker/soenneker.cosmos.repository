using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Be sure to pass a query that was built via <see cref="BuildPagedQueryable"/>
    /// </summary>
    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    ValueTask ExecuteOnGetItemsPaged(IQueryable<TDocument> queryable, Func<List<TDocument>, ValueTask> resultTask);

    /// <summary>
    /// Be sure to pass a query that was built via <see cref="BuildPagedQueryable"/>
    /// </summary>
    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    ValueTask ExecuteOnGetItemsPaged<T>(IQueryable<T> queryable, Func<List<T>, ValueTask> resultTask);

    /// <summary>
    /// Wraps <see cref="GetAllPaged"/> and hides away the continuationToken logic in a do-while.
    /// </summary>
    ValueTask ExecuteOnGetAllPaged(int pageSize, Func<List<TDocument>, ValueTask> resultTask);

    ValueTask ExecuteOnGetItemsPaged(QueryDefinition queryDefinition, int pageSize, Func<List<TDocument>, ValueTask> resultTask);
    
}
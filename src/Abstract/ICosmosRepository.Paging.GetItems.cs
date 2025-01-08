using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Constants.Data;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    [Pure]
    ValueTask<(List<TDocument>, string?)> GetAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null, CancellationToken cancellationToken = default);

    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    [Pure]
    ValueTask<(List<TDocument>, string?)> GetItemsPaged(QueryDefinition queryDefinition, int pageSize, string? continuationToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Be sure to pass a query that was built via <see cref="BuildPagedQueryable"/>
    /// </summary>
    /// <remarks>
    /// NOTE! Make sure you have an ORDER clause in your query or the continuation token functionality may not work
    /// </remarks>
    [Pure]
    ValueTask<(List<T> items, string? continuationToken)> GetItemsPaged<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
}
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Constants.Data;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <inheritdoc cref="BuildQueryable{T}"/>
    [Pure]
    ValueTask<IQueryable<TDocument>> BuildQueryable(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an empty query that can utilize LINQ for the container that the repository belongs to. Does not actually query.
    /// </summary>
    [Pure]
    ValueTask<IQueryable<T>> BuildQueryable<T>(CancellationToken cancellationToken = default);

    ///<inheritdoc cref="BuildPagedQueryable{T}"/>
    [Pure]
    ValueTask<IQueryable<TDocument>> BuildPagedQueryable(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an empty query that can utilize LINQ, specifying the Cosmos requestOptions. Does not actually query. <para/>
    /// Be sure to order in your query. Leverage QueryableExtension.ToOrdered{IQueryable}/>
    /// </summary>
    [Pure]
    ValueTask<IQueryable<T>> BuildPagedQueryable<T>(int pageSize = DataConstants.DefaultCosmosPageSize, string? continuationToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Essentially wraps <see cref="GetItems{T}(string, double?, CancellationToken)"/> with .FirstOrDefault()
    /// </summary>
    [Pure]
    ValueTask<T?> GetItem<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Will always return a non-null list. It may or may not have items.
    /// </summary>
    [Pure]
    ValueTask<List<T>> GetItems<T>(IQueryable<T> query, double? delayMs = null, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<int> Count(CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<int> Count(IQueryable<TDocument> query, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<bool> Any(CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<bool> None(CancellationToken cancellationToken = default);
}
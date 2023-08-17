using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Azure.Cosmos;
using Soenneker.Dtos.IdPartitionPair;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Careful, could be heavy. You may want <see cref="GetAllPaged"/> if the number of items are large (due to app memory limitations)
    /// </summary>
    [Pure] 
    ValueTask<List<TDocument>> GetAll(double? delayMs = null);

    /// <summary>
    /// Careful, could be heavy. You may want <see cref="GetAllPaged"/> if the number of items are large (due to app memory limitations)
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetAllByPartitionKey(string partitionKey, double? delayMs = null);

    /// <summary>
    /// Get items given a string SQL query directly. Typically should avoid (use specification, parameterization concerns, etc)
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetItems(string query, double? delayMs = null);

    [Pure]
    ValueTask<List<TDocument>> GetAllByDocumentIds(IEnumerable<string> ids);

    /// <summary>
    /// <inheritdoc cref="GetItems(string, double?)"/> <para/>
    /// Includes deserialization.
    /// </summary>
    [Pure]
    ValueTask<List<T>> GetItems<T>(string query, double? delayMs = null);

    [Pure]
    ValueTask<List<T>> GetItems<T, TResponse>(ODataQueryOptions odataOptions);

    [Pure]
    ValueTask<List<TDocument>> GetItems<TResponse>(ODataQueryOptions odataOptions);

    [Pure]
    ValueTask<List<T>> GetItems<T, TResponse>(ODataQueryOptions odataOptions, IQueryable query);

    [Pure]
    ValueTask<List<TDocument>> GetItems(QueryDefinition queryDefinition, double? delayMs = null);

    /// <summary>
    /// The bottom method call for most GetItems() in ICosmosRepository
    /// </summary>
    [Pure]
    ValueTask<List<T>> GetItems<T>(QueryDefinition queryDefinition, double? delayMs = null);

    /// <summary>
    /// Retrieves a list of items with createdAt between the parameters (inclusive, careful). Non-ordered.
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetItemsBetween(DateTime startAt, DateTime endAt, double? delayMs = null);

    [Pure]
    ValueTask<List<IdPartitionPair>> GetAllIds(double? delayMs = null);

    /// <summary>
    /// Before executing, adds an additional where clause to only gather ids from a given queryable (useful say during deletion)
    /// </summary>
    [Pure]
    ValueTask<List<IdPartitionPair>> GetIds(IQueryable<TDocument> query, double? delayMs = null);

    [Pure]
    ValueTask<List<string>> GetAllPartitionKeys(double? delayMs = null);

    /// <summary>
    /// Before executing, adds an additional where clause to only gather partitionKeys from a given queryable
    /// </summary>
    [Pure]
    ValueTask<List<string>> GetPartitionKeys(IQueryable<TDocument> query, double? delayMs = null);
}
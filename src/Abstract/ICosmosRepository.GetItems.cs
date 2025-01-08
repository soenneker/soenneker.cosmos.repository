using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Dtos.IdNamePair;
using Soenneker.Dtos.IdPartitionPair;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Careful, could be heavy. You may want <see cref="GetAllPaged"/> if the number of items are large (due to app memory limitations)
    /// </summary>
    [Pure] 
    ValueTask<List<TDocument>> GetAll(double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Careful, could be heavy. You may want <see cref="GetAllPaged"/> if the number of items are large (due to app memory limitations)
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetAllByPartitionKey(string partitionKey, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get items given a string SQL query directly. Typically should avoid (use specification, parameterization concerns, etc)
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetItems(string query, double? delayMs = null, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<List<TDocument>> GetAllByDocumentIds(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<List<TDocument>?> GetAllByIdNamePairs(IEnumerable<IdNamePair> pairs, CancellationToken cancellationToken = default);

    /// <summary>
    /// <inheritdoc cref="GetItems(string, double?, CancellationToken)"/> <para/>
    /// Includes deserialization.
    /// </summary>
    [Pure]
    ValueTask<List<T>> GetItems<T>(string query, double? delayMs = null, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<List<TDocument>> GetItems(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The bottom method call for most GetItems() in ICosmosRepository
    /// </summary>
    [Pure]
    ValueTask<List<T>> GetItems<T>(QueryDefinition queryDefinition, double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a list of items with createdAt between the parameters (inclusive, careful). Non-ordered.
    /// </summary>
    [Pure]
    ValueTask<List<TDocument>> GetItemsBetween(DateTime startAt, DateTime endAt, double? delayMs = null, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<List<IdPartitionPair>> GetAllIds(double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Before executing, adds an additional where clause to only gather ids from a given query (useful say during deletion)
    /// </summary>
    [Pure]
    ValueTask<List<IdPartitionPair>> GetIds(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default);

    [Pure]
    ValueTask<List<string>> GetAllPartitionKeys(double? delayMs = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Before executing, adds an additional where clause to only gather partitionKeys from a given query
    /// </summary>
    [Pure]
    ValueTask<List<string>> GetPartitionKeys(IQueryable<TDocument> query, double? delayMs = null, CancellationToken cancellationToken = default);
}
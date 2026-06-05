using Soenneker.Documents.Audit;
using Soenneker.Enums.CrudEventTypes;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the cosmos repository contract.
/// </summary>
/// <typeparam name="TDocument">The TDocument type.</typeparam>
public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Builds db event audit record.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>The result of the operation.</returns>
    [Pure]
    AuditDocument BuildDbEventAuditRecord(CrudEventType eventType, string entityId, object? entity, string? userId);

    /// <summary>
    /// Look up the user (if it exists), create an Audit document, and add it to the audit container.
    /// Always uses the queue
    /// </summary>
    ValueTask CreateAuditItem(CrudEventType eventType, string entityId, object? item = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Look up the user (if it exists), create an Audit document, and add it to the audit container.
    /// Always uses the queue
    /// </summary>
    ValueTask CreateAuditItem(CrudEventType eventType, string entityId, string entityJson, CancellationToken cancellationToken = default);
}
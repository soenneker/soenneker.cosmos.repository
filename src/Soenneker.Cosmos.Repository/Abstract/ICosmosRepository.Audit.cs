using Soenneker.Documents.Audit;
using Soenneker.Enums.CrudEventTypes;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Builds db event audit record.
    /// </summary>
    /// <param name="eventType">Event Type for the build db event audit record operation.</param>
    /// <param name="entityId">Identifier of the entity to target.</param>
    /// <param name="entity">Entity for the build db event audit record operation.</param>
    /// <param name="userId">Identifier of the user to target.</param>
    /// <returns>The resulting audit Document.</returns>
    [Pure]
    AuditDocument BuildDbEventAuditRecord(CrudEventType eventType, string entityId, object? entity, string? userId);

    /// <summary>
    /// Look up the user (if it exists), create an Audit document, and add it to the audit container.
    /// Always uses the queue
    /// </summary>
    /// <param name="eventType">Event Type for the create audit item operation.</param>
    /// <param name="entityId">Identifier of the entity to target.</param>
    /// <param name="item">Receives the entry when the key is found.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the audit item creation is complete.</returns>
    ValueTask CreateAuditItem(CrudEventType eventType, string entityId, object? item = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Look up the user (if it exists), create an Audit document, and add it to the audit container.
    /// Always uses the queue
    /// </summary>
    /// <param name="eventType">Event Type for the create audit item operation.</param>
    /// <param name="entityId">Identifier of the entity to target.</param>
    /// <param name="entityJson">Entity JSON for the create audit item operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the audit item creation is complete.</returns>
    ValueTask CreateAuditItem(CrudEventType eventType, string entityId, string entityJson, CancellationToken cancellationToken = default);
}

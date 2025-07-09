using Soenneker.Documents.Audit;
using Soenneker.Enums.CrudEventTypes;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    [Pure]
    AuditDocument BuildDbEventAuditRecord(CrudEventType eventType, string entityId, object? entity, string? userId);

    /// <summary>
    /// Look up the user (if it exists), create an Audit document, and add it to the audit container.
    /// Always uses the queue
    /// </summary>
    ValueTask CreateAuditItem(CrudEventType eventType, string entityId, object? item = null, CancellationToken cancellationToken = default);
}
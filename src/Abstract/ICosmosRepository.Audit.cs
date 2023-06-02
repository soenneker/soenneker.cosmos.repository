using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using Soenneker.Documents.Audit;
using Soenneker.Enums.EventType;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    [Pure]
    AuditDocument BuildDbEventAuditRecord(EventType eventType, string entityId, object? entity, string? userId);

    /// <summary>
    /// Look up the user (if it exists), create an Audit document, and add it to the audit container.
    /// Always uses the queue
    /// </summary>
    ValueTask CreateAuditItem(EventType eventType, string entityId, object? item = null);
}
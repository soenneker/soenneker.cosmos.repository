using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Audit;
using Soenneker.Documents.Document;
using Soenneker.Enums.EventType;
using Soenneker.Enums.JsonOptions;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Json;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public AuditDocument BuildDbEventAuditRecord(EventType eventType, string entityId, object? entity, string? userId)
    {
        // The PartitionKey of the AuditRow is the Document Id of the target entity
        string partitionKey = entityId.ToSplitId().DocumentId;

        var audit = new AuditDocument
        {
            DocumentId = Guid.NewGuid().ToString(),
            PartitionKey = partitionKey,
            EntityId = entityId,
            EntityType = typeof(TDocument).Name,
            Entity = entity,
            EventType = eventType,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        return audit;
    }

    public ValueTask CreateAuditItem(EventType eventType, string entityId, object? item = null, CancellationToken cancellationToken = default)
    {
        string? userId = _userContext.GetIdSafe();

        AuditDocument auditItem = BuildDbEventAuditRecord(eventType, entityId, item, userId);

        if (_auditLog)
        {
            string? serialized = JsonUtil.Serialize(auditItem, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        ValueTask result = _backgroundQueue.QueueValueTask(async token =>
        {
            Microsoft.Azure.Cosmos.Container container = await AuditContainer(token).NoSync();

           await container.CreateItemAsync(auditItem, new PartitionKey(auditItem.PartitionKey), ExcludeRequestOptions, token).NoSync();
        }, cancellationToken);

        return result;
    }
}
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Documents.Audit;
using Soenneker.Documents.Document;
using Soenneker.Enums.CrudEventTypes;
using Soenneker.Enums.JsonOptions;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Json;
using Soenneker.Utils.Method;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public AuditDocument BuildDbEventAuditRecord(CrudEventType eventType, string entityId, object? entity, string? userId)
    {
        // The PartitionKey of the AuditRow is the Document Id of the target entity
        string partitionKey = entityId.ToSplitId()
                                      .DocumentId;

        return new AuditDocument
        {
            DocumentId = Guid.NewGuid()
                             .ToString(),
            PartitionKey = partitionKey,
            EntityId = entityId,
            EntityType = typeof(TDocument).Name,
            Entity = entity,
            EventType = eventType,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public async ValueTask CreateAuditItem(CrudEventType eventType, string entityId, object? item = null, CancellationToken cancellationToken = default)
    {
        string? userId = _userContext.GetIdSafe();
        await CreateAndQueueAuditItem(eventType, entityId, item, userId, cancellationToken).NoSync();
    }


    public async ValueTask CreateAuditItem(CrudEventType eventType, string entityId, string entityJson, CancellationToken cancellationToken = default)
    {
        string? userId = _userContext.GetIdSafe();
        if (entityJson.HasContent())
        {
            using JsonDocument doc = JsonDocument.Parse(entityJson);
            await CreateAndQueueAuditItem(eventType, entityId, doc.RootElement, userId, cancellationToken).NoSync();
            return;
        }

        await CreateAndQueueAuditItem(eventType, entityId, null, userId, cancellationToken).NoSync();
    }

    private async ValueTask CreateAuditItemFromUtf8(CrudEventType eventType, string entityId, ReadOnlyMemory<byte> entityJson,
        CancellationToken cancellationToken)
    {
        string? userId = _userContext.GetIdSafe();

        if (!entityJson.IsEmpty)
        {
            using JsonDocument doc = JsonDocument.Parse(entityJson);
            await CreateAndQueueAuditItem(eventType, entityId, doc.RootElement, userId, cancellationToken).NoSync();
            return;
        }

        await CreateAndQueueAuditItem(eventType, entityId, null, userId, cancellationToken).NoSync();
    }

    private async ValueTask CreateAndQueueAuditItem(CrudEventType eventType, string entityId, object? entity, string? userId,
        CancellationToken cancellationToken)
    {
        AuditDocument auditItem = BuildDbEventAuditRecord(eventType, entityId, entity, userId);

        if (_auditLog && Logger.IsEnabled(LogLevel.Debug))
        {
            string? serialized = JsonUtil.Serialize(auditItem, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        byte[] json = JsonUtil.SerializeToUtf8Bytes(auditItem, JsonOptionType.Web);
        await QueueAuditItem(json, auditItem.PartitionKey, cancellationToken).NoSync();
    }

    private async ValueTask QueueAuditItem(byte[] json, string partitionKey, CancellationToken cancellationToken)
    {
        Microsoft.Azure.Cosmos.Container container = await AuditContainer(cancellationToken).NoSync();

        await _backgroundQueue.QueueValueTask(
                                  (Container: container, PartitionKey: new PartitionKey(partitionKey), Json: json,
                                      Options: CosmosRequestOptions.ExcludeResponse),
                                  static async (s, token) =>
                                  {
                                      using var ms = new MemoryStream(s.Json, writable: false);

                                      using ResponseMessage resp = await s.Container.CreateItemStreamAsync(ms, s.PartitionKey, s.Options, token)
                                                                          .NoSync();

                                      resp.EnsureSuccessStatusCode();
                                  }, cancellationToken)
                              .NoSync();
    }
}

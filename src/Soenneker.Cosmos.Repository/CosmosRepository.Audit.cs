using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Documents.Audit;
using Soenneker.Documents.Document;
using Soenneker.Enums.CrudEventTypes;
using Soenneker.Enums.JsonLibrary;
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
        string entityTypeName = typeof(TDocument).Name;

        AuditDocument auditItem = BuildDbEventAuditRecord(eventType, entityId, item, userId);

        if (_auditLog && Logger.IsEnabled(LogLevel.Debug))
        {
            string? serialized = JsonUtil.Serialize(auditItem, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), entityTypeName, serialized);
        }

        await QueueAuditItem(auditItem, cancellationToken)
            .NoSync();
    }


    public async ValueTask CreateAuditItem(CrudEventType eventType, string entityId, string entityJson, CancellationToken cancellationToken = default)
    {
        string? userId = _userContext.GetIdSafe();
        string entityTypeName = typeof(TDocument).Name;

        object? entity = null;

        if (entityJson.HasContent())
        {
            using JsonDocument doc = JsonDocument.Parse(entityJson);
            entity = doc.RootElement.Clone();
        }

        AuditDocument auditItem = BuildDbEventAuditRecord(eventType, entityId, entity, userId);

        if (_auditLog && Logger.IsEnabled(LogLevel.Debug))
        {
            string? serialized = JsonUtil.Serialize(auditItem, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), entityTypeName, serialized);
        }

        await QueueAuditItem(auditItem, cancellationToken)
            .NoSync();
    }

    private async ValueTask QueueAuditItem(AuditDocument auditItem, CancellationToken cancellationToken)
    {
        Microsoft.Azure.Cosmos.Container container = await AuditContainer(cancellationToken)
            .NoSync();

        string? json = JsonUtil.Serialize(auditItem, JsonOptionType.Web, JsonLibraryType.SystemTextJson);
        PartitionKey partitionKey = new(auditItem.PartitionKey);
        ItemRequestOptions options = CosmosRequestOptions.ExcludeResponse;

        await _backgroundQueue.QueueValueTask(
                                  (Container: container, PartitionKey: partitionKey, Json: json, Options: options, MemoryStreamUtil: _memoryStreamUtil),
                                  static async (s, token) =>
                                  {
                                      using MemoryStream ms = await s.MemoryStreamUtil.Get(s.Json, token)
                                                                     .NoSync();

                                      using ResponseMessage resp = await s.Container.CreateItemStreamAsync(ms, s.PartitionKey, s.Options, token)
                                                                          .NoSync();

                                      resp.EnsureSuccessStatusCode();
                                  }, cancellationToken)
                              .NoSync();
    }
}
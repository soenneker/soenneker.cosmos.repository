using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.RequestOptions;
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
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual ValueTask<string> AddItem(TDocument document, bool useQueue = false, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        if (_log && Logger.IsEnabled(LogLevel.Debug))
        {
            string? serialized = JsonUtil.Serialize(document, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {document}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        return InternalAddItem(document, useQueue, excludeResponse, cancellationToken);
    }

    private async ValueTask<string> InternalAddItem(TDocument document, bool useQueue, bool excludeResponse, CancellationToken cancellationToken)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        return await InternalAddItemWithContainer(document, container, useQueue, excludeResponse, cancellationToken);
    }

    private async ValueTask<string> InternalAddItemWithContainer(TDocument document, Microsoft.Azure.Cosmos.Container container, bool useQueue,
        bool excludeResponse, CancellationToken cancellationToken)
    {
        if (document.PartitionKey.IsNullOrWhiteSpace() || document.DocumentId.IsNullOrWhiteSpace())
            throw new Exception("DocumentId and PartitionKey MUST be present on the object before storing");

        ItemRequestOptions? options = excludeResponse ? CosmosRequestOptions.ExcludeResponse : null;

        if (useQueue)
        {
            // Snapshot everything we need up-front (no capturing document in the queued work item)
            string id = document.Id;
            string pk = document.PartitionKey;
            byte[] json = JsonUtil.SerializeToUtf8Bytes(document, JsonOptionType.Web);
            var partitionKey = new PartitionKey(pk);
            bool auditEnabled = AuditEnabled;

            await _backgroundQueue.QueueValueTask(
                                      (Self: this, Container: container, PartitionKey: partitionKey, Json: json, Options: options,
                                          AuditEnabled: auditEnabled, Id: id), static async (s, token) =>
                                      {
                                          using var ms = new MemoryStream(s.Json, writable: false);

                                          using ResponseMessage resp = await s.Container.CreateItemStreamAsync(ms, s.PartitionKey, s.Options, token)
                                                                              .NoSync();

                                          resp.EnsureSuccessStatusCode();

                                          if (s.AuditEnabled)
                                          {
                                              await s.Self.CreateAuditItemFromUtf8(CrudEventType.Create, s.Id, s.Json, token)
                                                     .NoSync();
                                          }
                                      }, cancellationToken)
                                  .NoSync();

            return id;
        }

        await container.CreateItemAsync(document, new PartitionKey(document.PartitionKey), options, cancellationToken)
                       .NoSync();

        if (AuditEnabled)
            await CreateAuditItem(CrudEventType.Create, document.Id, document, cancellationToken)
                .NoSync();

        return document.Id;
    }
}

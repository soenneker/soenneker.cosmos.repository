using System.IO;
using System.Runtime.CompilerServices;
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
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Enums.JsonLibrary;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDocument> UpdateItem(TDocument item, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
        return UpdateItem(item.Id, item, useQueue, excludeResponse, cancellationToken);
    }

    public async ValueTask<TDocument> UpdateItem(string id, TDocument item, bool useQueue = false, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        bool auditEnabled = AuditEnabled;

        if (_log && Logger.IsEnabled(LogLevel.Debug))
        {
            string? serialized = JsonUtil.Serialize(item, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        (string partitionKey, string documentId) = id.ToSplitId();

        PartitionKey pk = new(partitionKey);
        ItemRequestOptions? options = excludeResponse ? CosmosRequestOptions.ExcludeResponse : null;

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        if (useQueue)
        {
            string? itemJson = JsonUtil.Serialize(item, JsonOptionType.Web, JsonLibraryType.SystemTextJson);

            await _backgroundQueue.QueueValueTask(
                                      (Container: container, DocumentId: documentId, PartitionKey: pk, Json: itemJson, Options: options,
                                          MemoryStreamUtil: _memoryStreamUtil, AuditEnabled: auditEnabled, FullId: id, Self: this), static async (s, token) =>
                                      {
                                          using MemoryStream ms = await s.MemoryStreamUtil.Get(s.Json, token)
                                                                         .NoSync();

                                          using ResponseMessage resp = await s
                                                                             .Container.ReplaceItemStreamAsync(ms, s.DocumentId, s.PartitionKey, s.Options,
                                                                                 token)
                                                                             .NoSync();

                                          resp.EnsureSuccessStatusCode();

                                          if (s.AuditEnabled)
                                          {
                                              await s.Self.CreateAuditItem(CrudEventType.Update, s.FullId, s.Json, token)
                                                     .NoSync();
                                          }
                                      }, cancellationToken)
                                  .NoSync();

            return item;
        }

        ItemResponse<TDocument> response = await container.ReplaceItemAsync(item, documentId, pk, options, cancellationToken)
                                                          .NoSync();

        if (auditEnabled)
        {
            await CreateAuditItem(CrudEventType.Update, id, item, cancellationToken)
                .NoSync();
        }

        return response.Resource ?? item;
    }
}
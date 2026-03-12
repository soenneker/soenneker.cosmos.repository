using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.ConcurrentProcessing.Executor;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Documents.Document;
using Soenneker.Enums.CrudEventTypes;
using Soenneker.Enums.JsonLibrary;
using Soenneker.Enums.JsonOptions;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Delay;
using Soenneker.Utils.Json;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    // Avoids container lookup per item, thus not using UpdateItem
    public async ValueTask<List<TDocument>> UpdateItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        // Fetch the container once
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        TimeSpan? timespanDelay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        for (var i = 0; i < documents.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TDocument item = documents[i];

            if (_log)
            {
                string? serialized = JsonUtil.Serialize(item, JsonOptionType.Pretty);
                Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
            }

            // Parse ID into partition key and document ID
            (string partitionKey, string documentId) = item.Id.ToSplitId();

            // Precompute request options
            ItemRequestOptions? options = excludeResponse ? CosmosRequestOptions.ExcludeResponse : null;

            if (useQueue)
            {
                string itemId = item.Id;
                string json = JsonUtil.Serialize(item, JsonOptionType.Web, JsonLibraryType.SystemTextJson);
                var pk = new PartitionKey(partitionKey);

                // Snapshot AuditEnabled once if you want; or evaluate at execution time.
                bool auditEnabled = AuditEnabled;

                await _backgroundQueue.QueueValueTask(
                                          (Container: container, DocumentId: documentId, PartitionKey: pk, Json: json, Options: options,
                                              MemoryStreamUtil: _memoryStreamUtil, AuditEnabled: auditEnabled, Self: this, ItemId: itemId),
                                          static async (s, token) =>
                                          {
                                              using MemoryStream ms = await s.MemoryStreamUtil.Get(s.Json, token)
                                                                             .NoSync();

                                              using ResponseMessage resp = await s
                                                                                 .Container.ReplaceItemStreamAsync(ms, s.DocumentId, s.PartitionKey, s.Options,
                                                                                     token)
                                                                                 .NoSync();

                                              resp.EnsureSuccessStatusCode();

                                              if (s.AuditEnabled)
                                                  await s.Self.CreateAuditItem(CrudEventType.Update, s.ItemId, /* entity */ null, token)
                                                         .NoSync();
                                          }, cancellationToken)
                                      .NoSync();
            }
            else
            {
                ItemResponse<TDocument>? response = await container
                                                          .ReplaceItemAsync(item, documentId, new PartitionKey(partitionKey), options, cancellationToken)
                                                          .NoSync();

                if (AuditEnabled)
                    await CreateAuditItem(CrudEventType.Update, item.Id, item, cancellationToken)
                        .NoSync();

                // Update the document in the original list
                documents[i] = response.Resource ?? item;
            }

            if (timespanDelay.HasValue)
                await DelayUtil.Delay(timespanDelay.Value, null, cancellationToken)
                               .NoSync();
        }

        return documents;
    }

    public async ValueTask<List<TDocument>> UpdateItemsParallel(List<TDocument> documents, int maxConcurrency, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        var executor = new ConcurrentProcessingExecutor(maxConcurrency, Logger);

        bool auditEnabled = AuditEnabled;
        ItemRequestOptions? options = excludeResponse ? CosmosRequestOptions.ExcludeResponse : null;

        var states = new List<UpdateState>(documents.Count);
        for (var i = 0; i < documents.Count; i++)
        {
            states.Add(new UpdateState(Self: this, Container: container, Documents: documents, Index: i, Options: options, AuditEnabled: auditEnabled,
                Log: _log));
        }

        await executor.Execute(states, static async (s, ct) =>
                      {
                          ct.ThrowIfCancellationRequested();

                          // Read current item at execution time (in case caller mutated the list before execution starts)
                          TDocument item = s.Documents[s.Index];

                          try
                          {
                              if (s.Log)
                              {
                                  string? serialized = JsonUtil.Serialize(item, JsonOptionType.Pretty);
                                  s.Self.Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
                              }

                              (string partitionKey, string documentId) = item.Id.ToSplitId();

                              ItemResponse<TDocument> response = await s
                                                                       .Container.ReplaceItemAsync(item, documentId, new PartitionKey(partitionKey), s.Options,
                                                                           ct)
                                                                       .NoSync();

                              // Audit only after Replace succeeds
                              if (s.AuditEnabled)
                                  await s.Self.CreateAuditItem(CrudEventType.Update, item.Id, item, ct)
                                         .NoSync();

                              // Safe: each state writes to a unique index
                              s.Documents[s.Index] = response.Resource ?? item;
                          }
                          catch (Exception ex)
                          {
                              s.Self.Logger.LogError(ex, "Error updating document with ID: {id}", item.Id);
                          }
                      }, cancellationToken)
                      .NoSync();

        return documents;
    }

    private readonly record struct UpdateState(
        CosmosRepository<TDocument> Self,
        Microsoft.Azure.Cosmos.Container Container,
        List<TDocument> Documents,
        int Index,
        ItemRequestOptions? Options,
        bool AuditEnabled,
        bool Log);
}
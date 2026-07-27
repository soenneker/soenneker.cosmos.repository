using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.ConcurrentProcessing.Executor;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Cosmos.Repository.Dtos;
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
        return await UpdateItemsCore(documents, delayMs, useQueue, excludeResponse, cancellationToken).NoSync();
    }

    public async ValueTask<List<CosmosItem<TDocument>>> UpdateItemsIfMatch(List<CosmosItem<TDocument>> items, double? delayMs = null,
        CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CosmosItem<TDocument> item = items[i];
            ArgumentException.ThrowIfNullOrWhiteSpace(item.ETag);

            items[i] = await UpdateItemIfMatchWithContainer(container, GetRequiredId(item.Document), item.Document, item.ETag, cancellationToken).NoSync();

            if (delay.HasValue)
                await DelayUtil.Delay(delay.Value, null, cancellationToken).NoSync();
        }

        return items;
    }

    private async ValueTask<List<TDocument>> UpdateItemsCore(List<TDocument> documents, double? delayMs, bool useQueue, bool excludeResponse,
        CancellationToken cancellationToken)
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
                string? json = JsonUtil.Serialize(item, JsonOptionType.Web, JsonLibraryType.SystemTextJson);
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
        return await UpdateItemsParallelCore(documents, maxConcurrency, excludeResponse, cancellationToken).NoSync();
    }

    public async ValueTask<List<CosmosItem<TDocument>>> UpdateItemsParallelIfMatch(List<CosmosItem<TDocument>> items, int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        var executor = new ConcurrentProcessingExecutor(maxConcurrency, Logger);

        var states = new List<ConditionalUpdateState>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(items[i].ETag);
            states.Add(new ConditionalUpdateState(this, container, items, i));
        }

        await executor.Execute(states, static async (s, ct) =>
                      {
                          CosmosItem<TDocument> item = s.Items[s.Index];
                          s.Items[s.Index] = await s.Self
                                                  .UpdateItemIfMatchWithContainer(s.Container, GetRequiredId(item.Document), item.Document, item.ETag, ct)
                                                  .NoSync();
                      }, cancellationToken)
                      .NoSync();

        return items;
    }

    private async ValueTask<List<TDocument>> UpdateItemsParallelCore(List<TDocument> documents, int maxConcurrency, bool excludeResponse,
        CancellationToken cancellationToken)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        var executor = new ConcurrentProcessingExecutor(maxConcurrency, Logger);

        bool auditEnabled = AuditEnabled;

        var states = new List<UpdateState>(documents.Count);
        for (var i = 0; i < documents.Count; i++)
        {
            TDocument document = documents[i];
            ItemRequestOptions? options = excludeResponse ? CosmosRequestOptions.ExcludeResponse : null;
            states.Add(new UpdateState(Self: this, Container: container, Documents: documents, Index: i, Options: options,
                AuditEnabled: auditEnabled, Log: _log));
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

    private readonly record struct ConditionalUpdateState(
        CosmosRepository<TDocument> Self,
        Microsoft.Azure.Cosmos.Container Container,
        List<CosmosItem<TDocument>> Items,
        int Index);
}

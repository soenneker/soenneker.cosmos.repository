﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.ConcurrentProcessing.Executor;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Documents.Document;
using Soenneker.Enums.CrudEventTypes;
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
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

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
                await _backgroundQueue.QueueValueTask(async token =>
                                      {
                                          await container.ReplaceItemAsync(item, documentId, new PartitionKey(partitionKey), options, token).NoSync();

                                          if (AuditEnabled)
                                              await CreateAuditItem(CrudEventType.Update, item.Id, item, token).NoSync();
                                      }, cancellationToken)
                                      .NoSync();
            }
            else
            {
                ItemResponse<TDocument>? response =
                    await container.ReplaceItemAsync(item, documentId, new PartitionKey(partitionKey), options, cancellationToken).NoSync();

                if (AuditEnabled)
                    await CreateAuditItem(CrudEventType.Update, item.Id, item, cancellationToken).NoSync();

                // Update the document in the original list
                documents[i] = response.Resource ?? item;
            }

            if (timespanDelay.HasValue)
                await DelayUtil.Delay(timespanDelay.Value, null, cancellationToken).NoSync();
        }

        return documents;
    }

    public async ValueTask<List<TDocument>> UpdateItemsParallel(List<TDocument> documents, int maxConcurrency, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        // Fetch the container once
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        var executor = new ConcurrentProcessingExecutor(maxConcurrency, Logger);
        var tasks = new List<Func<Task>>();

        for (var i = 0; i < documents.Count; i++)
        {
            int index = i; // Create a local copy of `i` to avoid closure issues

            tasks.Add(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    TDocument item = documents[index];

                    if (_log)
                    {
                        string? serialized = JsonUtil.Serialize(item, JsonOptionType.Pretty);
                        Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
                    }

                    // Parse ID into partition key and document ID
                    (string partitionKey, string documentId) = item.Id.ToSplitId();

                    // Precompute request options
                    ItemRequestOptions? options = excludeResponse ? CosmosRequestOptions.ExcludeResponse : null;

                    // Perform the update operation
                    ItemResponse<TDocument>? response = await container.ReplaceItemAsync(
                                                                           item, documentId, new PartitionKey(partitionKey), options, cancellationToken)
                                                                       .NoSync();

                    if (AuditEnabled)
                    {
                        await CreateAuditItem(CrudEventType.Update, item.Id, item, cancellationToken).NoSync();
                    }

                    // Update the document in the original list safely
                    documents[index] = response.Resource ?? item;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error updating document with ID: {id}", documents[index].Id);
                }
            });
        }

        // Execute all tasks in parallel with the given concurrency limit
        await executor.Execute(tasks, cancellationToken).NoSync();

        return documents;
    }
}
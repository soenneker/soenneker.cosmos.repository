using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Documents.Document;
using Soenneker.Enums.CrudEventTypes;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Delay;
using Soenneker.Utils.Json;
using Soenneker.Utils.Method;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public async ValueTask<List<TDocument>> PatchItems(List<TDocument> documents, List<PatchOperation> operations, double? delayMs = null,
        bool useQueue = false, CancellationToken cancellationToken = default)
    {
        // Precompute delay once
        TimeSpan? timespanDelay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        if (timespanDelay.HasValue)
        {
            foreach (TDocument item in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await PatchItem(item.Id, operations, useQueue, cancellationToken)
                    .NoSync();
                await DelayUtil.Delay(timespanDelay.Value, null, cancellationToken)
                               .NoSync();
            }
        }
        else
        {
            foreach (TDocument item in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await PatchItem(item.Id, operations, useQueue, cancellationToken)
                    .NoSync();
            }
        }

        return documents;
    }

    public async ValueTask<TDocument?> PatchItem(string id, List<PatchOperation> operations, bool useQueue = false,
        CancellationToken cancellationToken = default)
    {
        if (_log)
            Logger.LogDebug("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        (string partitionKey, string documentId) = id.ToSplitId();

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        if (useQueue)
        {
            // Snapshot ops so we don't retain caller's List/backing array.
            PatchOperation[] ops = operations.Count == 0 ? [] : operations.ToArray();

            bool auditEnabled = AuditEnabled;

            await _backgroundQueue.QueueValueTask(
                                      (Self: this, Container: container, PartitionKey: partitionKey, DocumentId: documentId, Ops: ops,
                                          AuditEnabled: auditEnabled, FullId: id), static async (s, token) =>
                                      {
                                          // This will throw on non-success (Cosmos SDK throws CosmosException)
                                          ItemResponse<TDocument> resp = await s
                                                                               .Container.PatchItemAsync<TDocument>(s.DocumentId,
                                                                                   new PartitionKey(s.PartitionKey), s.Ops, cancellationToken: token)
                                                                               .NoSync();

                                          // Audit only after success
                                          if (s.AuditEnabled)
                                          {
                                              await s.Self.CreateAuditItem(CrudEventType.Update, s.FullId, cancellationToken: token)
                                                     .NoSync();
                                          }
                                      }, cancellationToken)
                                  .NoSync();

            return null;
        }

        ItemResponse<TDocument> response = await container
                                                 .PatchItemAsync<TDocument>(documentId, new PartitionKey(partitionKey), operations, requestOptions: null,
                                                     cancellationToken: cancellationToken)
                                                 .NoSync();

        if (AuditEnabled)
            await CreateAuditItem(CrudEventType.Update, id, response.Resource, cancellationToken)
                .NoSync();

        return response.Resource;
    }
}
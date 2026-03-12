using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.ConcurrentProcessing.Executor;
using Soenneker.Documents.Document;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Delay;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask<List<TDocument>> AddItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false,
        bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
        if (_log)
        {
            Logger.LogDebug("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name,
                delayMs.GetValueOrDefault());
        }

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        if (delayMs.HasValue)
        {
            TimeSpan timeSpanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

            foreach (TDocument item in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                item.Id = await InternalAddItemWithContainer(item, container, useQueue, excludeResponse, cancellationToken)
                    .NoSync();
                await DelayUtil.Delay(timeSpanDelay, null, cancellationToken)
                               .NoSync();
            }
        }
        else
        {
            foreach (TDocument item in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                item.Id = await InternalAddItemWithContainer(item, container, useQueue, excludeResponse, cancellationToken)
                    .NoSync();
            }
        }

        return documents;
    }

    public virtual async ValueTask<List<TDocument>> AddItemsParallel(List<TDocument> documents, int maxConcurrency, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        if (_log)
            Logger.LogDebug("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        var executor = new ConcurrentProcessingExecutor(maxConcurrency, Logger);

        // Build state list (no per-item closures)
        var states = new List<AddItemState>(documents.Count);
        for (var i = 0; i < documents.Count; i++)
        {
            states.Add(new AddItemState(Self: this, Container: container, Document: documents[i], ExcludeResponse: excludeResponse));
        }

        await executor.Execute(states, static async (s, ct) =>
                      {
                          ct.ThrowIfCancellationRequested();

                          s.Document.Id = await s.Self.InternalAddItemWithContainer(s.Document, s.Container, useQueue: false,
                                                     excludeResponse: s.ExcludeResponse, cancellationToken: ct)
                                                 .NoSync();
                      }, cancellationToken)
                      .NoSync();

        return documents;
    }

    private readonly record struct AddItemState(
        CosmosRepository<TDocument> Self,
        Microsoft.Azure.Cosmos.Container Container,
        TDocument Document,
        bool ExcludeResponse);
}
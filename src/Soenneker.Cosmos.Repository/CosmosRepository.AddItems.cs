using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.ConcurrentProcessing.Executor;
using Soenneker.Documents.Document;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Delay;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask<List<TDocument>> AddItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false,
        bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
            return documents;

        if (_log && Logger.IsEnabled(LogLevel.Debug))
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
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        if (documents.Count == 0)
            return documents;

        if (_log && Logger.IsEnabled(LogLevel.Debug))
            Logger.LogDebug("-- COSMOS: {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        var executor = new ConcurrentProcessingExecutor(maxConcurrency, Logger);

        await executor.Execute(documents, async (document, ct) =>
        {
            document.Id = await InternalAddItemWithContainer(document, container, useQueue: false,
                excludeResponse: excludeResponse, cancellationToken: ct).NoSync();
        }, cancellationToken).NoSync();

        return documents;
    }

}

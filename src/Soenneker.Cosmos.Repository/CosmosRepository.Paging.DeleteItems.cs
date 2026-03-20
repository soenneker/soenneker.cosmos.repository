using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Constants.Data;
using Soenneker.Documents.Document;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    private readonly record struct IdOnlyProjection(string DocumentId, string PartitionKey);

    public virtual async ValueTask DeleteAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null, bool useQueue = false,
        CancellationToken cancellationToken = default)
    {
        if (_log && Logger.IsEnabled(LogLevel.Warning))
        {
            Logger.LogWarning("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs",
                MethodUtil.Get(), typeof(TDocument).Name, delayMs.GetValueOrDefault());
        }

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        IQueryable<TDocument> query = await BuildPagedQueryable(pageSize, cancellationToken: cancellationToken).NoSync();
        query = query.OrderBy(static c => c.CreatedAt);

        IQueryable<IdOnlyProjection> projected = query.Select(static c => new IdOnlyProjection(c.DocumentId, c.PartitionKey));

        await ExecuteOnGetItemsPaged(projected, async results =>
        {
            if (_log && Logger.IsEnabled(LogLevel.Debug))
                Logger.LogDebug("Number of rows to be deleted in page: {rows}", results.Count);

            for (int i = 0; i < results.Count; i++)
            {
                IdOnlyProjection result = results[i];

                await DeleteItem(result.DocumentId, result.PartitionKey, useQueue, cancellationToken).NoSync();

                if (delay.HasValue)
                    await Task.Delay(delay.Value, cancellationToken).NoSync();
            }
        }, cancellationToken).NoSync();

        if (_log && Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
        }
    }

    public virtual async ValueTask DeleteItemsPaged(QueryDefinition queryDefinition, int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null,
        bool useQueue = false, CancellationToken cancellationToken = default)
    {
        if (_log && Logger.IsEnabled(LogLevel.Warning))
        {
            Logger.LogWarning("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs",
                MethodUtil.Get(), typeof(TDocument).Name, delayMs.GetValueOrDefault());
        }

        TimeSpan? delay = delayMs.HasValue ? TimeSpan.FromMilliseconds(delayMs.Value) : null;

        await ExecuteOnGetItemsPaged(queryDefinition, pageSize, async results =>
        {
            if (_log && Logger.IsEnabled(LogLevel.Debug))
                Logger.LogDebug("Number of rows to be deleted in page: {rows}", results.Count);

            for (int i = 0; i < results.Count; i++)
            {
                TDocument result = results[i];

                await DeleteItem(result.DocumentId, result.PartitionKey, useQueue, cancellationToken).NoSync();

                if (delay.HasValue)
                    await Task.Delay(delay.Value, cancellationToken).NoSync();
            }
        }, cancellationToken).NoSync();

        if (_log && Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
        }
    }
}
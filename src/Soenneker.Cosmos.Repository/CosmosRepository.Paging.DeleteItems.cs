using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Constants.Data;
using Soenneker.Documents.Document;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual async ValueTask DeleteAllPaged(int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null, bool useQueue = false, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name, delayMs.GetValueOrDefault());

        IQueryable<TDocument> query = await BuildPagedQueryable(pageSize, cancellationToken: cancellationToken)
            .NoSync();
        query = query.OrderBy(c => c.CreatedAt);

        var newQuery = query.Select(c => new {c.DocumentId, c.PartitionKey});

        await ExecuteOnGetItemsPaged(newQuery, async results =>
            {
                Logger.LogDebug("Number of rows to be deleted in page: {rows}", results.Count);

                foreach (var result in results)
                {
                    await DeleteItem(result.DocumentId, result.PartitionKey, useQueue, cancellationToken)
                        .NoSync();
                }
            }, cancellationToken)
            .NoSync();

        Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
    }

    public virtual async ValueTask DeleteItemsPaged<T>(QueryDefinition queryDefinition, int pageSize = DataConstants.DefaultCosmosPageSize, double? delayMs = null, bool useQueue = false,
        CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("-- COSMOS: {method} ({type}) w/ {delayMs}ms delay between docs", MethodUtil.Get(), typeof(TDocument).Name, delayMs.GetValueOrDefault());

        await ExecuteOnGetItemsPaged(queryDefinition, -1, async results =>
            {
                Logger.LogDebug("Number of rows to be deleted in page: {rows}", results.Count);

                foreach (TDocument result in results)
                {
                    await DeleteItem(result.DocumentId, result.PartitionKey, useQueue, cancellationToken)
                        .NoSync();
                }
            }, cancellationToken)
            .NoSync();

        Logger.LogDebug("-- COSMOS: Finished {method} ({type})", MethodUtil.Get(), typeof(TDocument).Name);
    }
}
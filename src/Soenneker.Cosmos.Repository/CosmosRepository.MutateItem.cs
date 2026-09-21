using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Soenneker.Cosmos.Repository.Dtos;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument>
{
    public ValueTask<TDocument?> MutateItem(string id, Func<TDocument, bool> mutation, CancellationToken cancellationToken = default,
        int maxAttempts = 5, CosmosReadOptions? readOptions = null)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        return MutateItem(id, document => new ValueTask<bool>(mutation(document)), cancellationToken, maxAttempts, readOptions);
    }

    public async ValueTask<TDocument?> MutateItem(string id, Func<TDocument, ValueTask<bool>> mutation,
        CancellationToken cancellationToken = default, int maxAttempts = 5, CosmosReadOptions? readOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        (string partitionKey, string documentId) = id.ToSplitId();
        var pk = new PartitionKey(partitionKey);
        var requestOptions = (readOptions ?? DefaultReadOptions)?.ToItemRequestOptions();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            CosmosItem<TDocument>? current = await GetItemWithETagCore(documentId, pk, requestOptions, cancellationToken).NoSync();
            if (current == null)
                return null;

            if (!await mutation(current.Document).NoSync())
                return current.Document;

            try
            {
                CosmosItem<TDocument> updated = await UpdateItemIfMatch(current, cancellationToken).NoSync();
                return updated.Document;
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed && attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5 * attempt), cancellationToken).NoSync();
            }
        }

        throw new InvalidOperationException("The optimistic concurrency retry limit was reached.");
    }
}

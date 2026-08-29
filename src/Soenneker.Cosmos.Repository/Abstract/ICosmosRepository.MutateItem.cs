using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> where TDocument : class
{
    /// <summary>
    /// Reads an item with its ETag, applies a mutation, and conditionally replaces it.
    /// A 412 response causes the latest item to be read and the mutation to be reapplied, up to <paramref name="maxAttempts"/> times.
    /// </summary>
    /// <param name="id">Identifier of the Cosmos Repository instance or registration to target.</param>
    /// <param name="mutation">Callback used by mutate item.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="maxAttempts">Max Attempts for the mutate item operation.</param>
    /// <returns>The current or updated document, or <see langword="null"/> when the item does not exist.</returns>
    /// <remarks>
    /// The mutation may be invoked more than once and must only describe the intended document delta.
    /// It must not perform non-idempotent external side effects.
    /// </remarks>
    ValueTask<TDocument?> MutateItem(string id, Func<TDocument, bool> mutation, CancellationToken cancellationToken = default,
        int maxAttempts = 5);

    /// <summary>
    /// Reads an item with its ETag, applies an asynchronous mutation, and conditionally replaces it.
    /// A 412 response causes the latest item to be read and the mutation to be reapplied, up to <paramref name="maxAttempts"/> times.
    /// </summary>
    /// <param name="id">Identifier of the Cosmos Repository instance or registration to target.</param>
    /// <param name="mutation">Callback used by mutate item.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <param name="maxAttempts">Max Attempts for the mutate item operation.</param>
    /// <returns>The current or updated document, or <see langword="null"/> when the item does not exist.</returns>
    /// <remarks>
    /// The mutation may be invoked more than once and must only describe the intended document delta.
    /// It must not perform non-idempotent external side effects.
    /// </remarks>
    ValueTask<TDocument?> MutateItem(string id, Func<TDocument, ValueTask<bool>> mutation,
        CancellationToken cancellationToken = default, int maxAttempts = 5);
}

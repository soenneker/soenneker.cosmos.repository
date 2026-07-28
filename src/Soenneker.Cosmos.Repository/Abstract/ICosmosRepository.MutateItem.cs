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
    /// <remarks>
    /// The mutation may be invoked more than once and must only describe the intended document delta.
    /// It must not perform non-idempotent external side effects.
    /// </remarks>
    /// <returns>The current or updated document, or <see langword="null"/> when the item does not exist.</returns>
    ValueTask<TDocument?> MutateItem(string id, Func<TDocument, bool> mutation, CancellationToken cancellationToken = default,
        int maxAttempts = 5);

    /// <summary>
    /// Reads an item with its ETag, applies an asynchronous mutation, and conditionally replaces it.
    /// A 412 response causes the latest item to be read and the mutation to be reapplied, up to <paramref name="maxAttempts"/> times.
    /// </summary>
    /// <remarks>
    /// The mutation may be invoked more than once and must only describe the intended document delta.
    /// It must not perform non-idempotent external side effects.
    /// </remarks>
    /// <returns>The current or updated document, or <see langword="null"/> when the item does not exist.</returns>
    ValueTask<TDocument?> MutateItem(string id, Func<TDocument, ValueTask<bool>> mutation,
        CancellationToken cancellationToken = default, int maxAttempts = 5);
}

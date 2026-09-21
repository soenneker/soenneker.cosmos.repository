namespace Soenneker.Cosmos.Repository.Dtos;

/// <summary>
/// Controls optimistic concurrency requirements for repository writes without allocating an options object.
/// </summary>
public readonly struct CosmosWriteOptions
{
    /// <summary>
    /// Gets whether updates, patches, and deletes must supply an ETag through an IfMatch method or MutateItem.
    /// Unconditional operations fail before accessing Cosmos or queuing work when enabled.
    /// Creates remain allowed because they cannot replace an existing item. False permits unconditional writes only when the repository does not require ETags.
    /// </summary>
    public bool RequireETag { get; init; }
}

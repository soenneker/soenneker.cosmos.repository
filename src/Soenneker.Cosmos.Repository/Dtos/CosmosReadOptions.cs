using Microsoft.Azure.Cosmos;

namespace Soenneker.Cosmos.Repository.Dtos;

/// <summary>
/// Optional consistency settings for a repository read. Unset properties preserve Cosmos SDK defaults.
/// </summary>
/// <remarks>
/// These settings do not protect subsequent writes; use ETag-conditional writes for optimistic concurrency.
/// SDK and account restrictions apply to each consistency setting.
/// This immutable value type can be reused across calls. An explicit empty value bypasses repository defaults and restores SDK defaults.
/// </remarks>
public readonly struct CosmosReadOptions
{
    /// <summary>
    /// Gets the optional read strategy. Null preserves the configured Cosmos SDK defaults.
    /// LatestCommitted guarantees locally committed data, not the latest write across regions.
    /// GlobalStrong requires a Strong-consistency account. SDK support and connection-mode restrictions apply.
    /// </summary>
    public ReadConsistencyStrategy? ReadConsistencyStrategy { get; init; }

    /// <summary>
    /// Gets the session token for the relevant partition, used with session consistency to read at least that version.
    /// A null token leaves session tracking to the Cosmos client.
    /// </summary>
    public string? SessionToken { get; init; }

    private bool IsEmpty => !ReadConsistencyStrategy.HasValue && SessionToken is null;

    internal ItemRequestOptions? ToItemRequestOptions() => IsEmpty ? null : new ItemRequestOptions
    {
        ReadConsistencyStrategy = ReadConsistencyStrategy,
        SessionToken = SessionToken
    };

    internal QueryRequestOptions? ToQueryRequestOptions() => IsEmpty ? null : new QueryRequestOptions
    {
        ReadConsistencyStrategy = ReadConsistencyStrategy,
        SessionToken = SessionToken
    };

    internal ReadManyRequestOptions? ToReadManyRequestOptions() => IsEmpty ? null : new ReadManyRequestOptions
    {
        ReadConsistencyStrategy = ReadConsistencyStrategy,
        SessionToken = SessionToken
    };
}

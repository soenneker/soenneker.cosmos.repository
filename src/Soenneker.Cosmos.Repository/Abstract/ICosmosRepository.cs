namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the core repository contract for a Cosmos DB document type.
/// </summary>
/// <remarks>
/// Repository methods that execute native Cosmos LINQ queries treat missing document properties as null
/// for supported literal null comparisons and nullable HasValue checks. Rewriting occurs after query composition,
/// including queries used to select documents for deletion. Explicit IsNull and IsDefined checks retain their meaning.
/// SQL strings and QueryDefinition inputs are not rewritten. Queries executed directly by callers must apply
/// Soenneker.Cosmos.Linq.WithNullSemantics themselves after composing filters.
/// Optional CosmosReadOptions control read consistency and session tokens without changing the account or client defaults.
/// When omitted, reads inherit DefaultReadOptions; its default null value retains existing behavior. For existing IQueryable inputs, configure read options when building
/// the query; the paged overload accepting an explicit page size and continuation token instead uses its own readOptions.
/// Consistency settings do not replace ETag-conditional writes and do not create a snapshot across multiple requests or pages.
/// </remarks>
public partial interface ICosmosRepository<TDocument> : ICosmosRepository where TDocument : class
{
    /// <summary>
    /// Gets repository-type read defaults. Null retains SDK defaults. Override on a derived repository to configure its reads.
    /// A non-null method option replaces the entire default; an explicit empty CosmosReadOptions restores SDK defaults.
    /// Explicit SDK query options likewise replace these defaults. Already-built queries retain their own options.
    /// </summary>
    Dtos.CosmosReadOptions? DefaultReadOptions { get; }

    /// <summary>
    /// Gets repository-type write defaults. Null retains existing behavior. Override to require ETags for updates, patches, and deletes.
    /// Method write options can require ETags but cannot disable a repository ETag requirement. IfMatch methods always enforce their supplied ETags.
    /// </summary>
    Dtos.CosmosWriteOptions? DefaultWriteOptions { get; }
}

/// <inheritdoc cref="ICosmosRepository{TDocument}"/>
/// <summary>
/// Provides non-generic access to Cosmos repository operations.
/// </summary>
public interface ICosmosRepository
{
}

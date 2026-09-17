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
/// </remarks>
public partial interface ICosmosRepository<TDocument> : ICosmosRepository where TDocument : class
{
}

/// <inheritdoc cref="ICosmosRepository{TDocument}"/>
/// <summary>
/// Provides non-generic access to Cosmos repository operations.
/// </summary>
public interface ICosmosRepository
{
}

namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// Defines the core repository contract for a Cosmos DB document type.
/// </summary>
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

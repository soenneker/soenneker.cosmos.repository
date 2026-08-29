namespace Soenneker.Cosmos.Repository.Abstract;

public partial interface ICosmosRepository<TDocument> : ICosmosRepository where TDocument : class
{
}

/// <inheritdoc cref="ICosmosRepository{TDocument}"/>
public interface ICosmosRepository
{
}

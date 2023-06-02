namespace Soenneker.Cosmos.Repository.Abstract;

/// <summary>
/// A repository is loosely linked with a container within Cosmos. (A repository MAY be the contents of an entire container, or it may be a subset)
/// </summary>
/// <typeparam name="TDocument"></typeparam>
public partial interface ICosmosRepository<TDocument> : ICosmosRepository where TDocument : class
{
}

/// <inheritdoc cref="ICosmosRepository{TDocument}"/>
public interface ICosmosRepository
{
}
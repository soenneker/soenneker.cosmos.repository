namespace Soenneker.Cosmos.Repository.Dtos;

/// <summary>
/// Represents a Cosmos DB item and the entity tag returned with it.
/// </summary>
/// <typeparam name="TDocument">The document type.</typeparam>
public sealed record CosmosItem<TDocument>(TDocument Document, string ETag);
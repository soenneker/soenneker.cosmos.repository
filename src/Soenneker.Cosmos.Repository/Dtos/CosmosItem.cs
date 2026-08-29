namespace Soenneker.Cosmos.Repository.Dtos;

public sealed record CosmosItem<TDocument>(TDocument Document, string ETag);

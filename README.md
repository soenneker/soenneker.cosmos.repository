[![](https://img.shields.io/nuget/v/Soenneker.Cosmos.Repository.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Cosmos.Repository/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.cosmos.repository/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.cosmos.repository/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Cosmos.Repository.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Cosmos.Repository/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.cosmos.repository/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.cosmos.repository/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Cosmos.Repository
### A data persistence abstraction layer for Cosmos DB

## Installation

```
dotnet add package Soenneker.Cosmos.Repository
```

## Optimistic concurrency

Read an item with its ETag, then use an explicit conditional write:

```csharp
CosmosItem<MyDocument>? item = await repository.GetItemWithETag(id, cancellationToken);

if (item is not null)
{
    item.Document.Name = "Updated";
    item = await repository.UpdateItemIfMatch(item, cancellationToken);
}
```

The returned wrapper contains the updated document and the new ETag required for another conditional write. If another writer changes the item between the read and write, Cosmos DB returns `412 Precondition Failed`.

Existing `UpdateItem`, `PatchItem`, and `DeleteItem` methods remain unconditional for backward compatibility. Conditional `*IfMatch` variants are immediate operations so they can return the new ETag and synchronously surface concurrency failures. Sequential and parallel conditional bulk updates accept and return `List<CosmosItem<TDocument>>`, keeping every ETag paired with its document.

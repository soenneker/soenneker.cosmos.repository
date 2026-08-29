[![](https://img.shields.io/nuget/v/Soenneker.Cosmos.Repository.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Cosmos.Repository/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.cosmos.repository/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.cosmos.repository/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Cosmos.Repository.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Cosmos.Repository/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.cosmos.repository/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.cosmos.repository/actions/workflows/codeql.yml)

# Soenneker.Cosmos.Repository

Defines the core repository contract for a Cosmos DB document type.

## Install

```bash
dotnet add package Soenneker.Cosmos.Repository
```

## Quick start

```csharp
using Soenneker.Cosmos.Repository.Abstract;

ICosmosRepository<TDocument> cosmosRepository = /* resolve from DI */;
var result = await cosmosRepository.AddItem(/* supply document */ default!, default);
```

Will throw exception if item id already exists.

## What you get

- `ICosmosRepository<TDocument>` — Defines the core repository contract for a Cosmos DB document type.
- `ICosmosRepository` — Provides non-generic access to Cosmos repository operations.
- `ICosmosRepositoryContext` — Defines the container level context.

## API at a glance

| API | What it does | Result / important behavior |
| --- | --- | --- |
| `ICosmosRepository<TDocument>.AddItem(document, useQueue, excludeResponse, cancellationToken)` | Will throw exception if item id already exists. | Fully qualified Id string (partitionKey:documentId). |
| `ICosmosRepository<TDocument>.AddItems(documents, delayMs, useQueue, excludeResponse, cancellationToken)` | Essentially just a helper that iterates over a list, calling `AddItem`. | The fully qualified IDs of the documents added by the operation. |
| `ICosmosRepository<TDocument>.CreateAuditItem(eventType, entityId, item, cancellationToken)` | Look up the user (if it exists), create an Audit document, and add it to the audit container. Always uses the queue. | A task that completes when the audit item creation is complete. |
| `ICosmosRepository<TDocument>.CreateAuditItem(eventType, entityId, entityJson, cancellationToken)` | Look up the user (if it exists), create an Audit document, and add it to the audit container. Always uses the queue. | A task that completes when the audit item creation is complete. |
| `ICosmosRepository<TDocument>.DeleteItemIfMatch(item, cancellationToken)` | Deletes a wrapped item only when its ETag still matches. Cosmos DB throws a 412 Precondition Failed response when the item has changed. | Completes only when the ETag still matches; Cosmos DB rejects a stale document with HTTP 412 Precondition Failed. |
| `ICosmosRepository<TDocument>.DeleteItem(entityId, useQueue, cancellationToken)` | Hard deletes one item by Id (partition and document, or one guid if they're the same). Will not throw. | A task that completes when the item deletion is complete. |
| `ICosmosRepository<TDocument>.DeleteItemIfMatch(entityId, expectedETag, cancellationToken)` | Deletes an item only when its current ETag matches `expectedETag`. Cosmos DB throws a 412 Precondition Failed response when the item has changed. | Completes only when the ETag still matches; Cosmos DB rejects a stale document with HTTP 412 Precondition Failed. |
| `ICosmosRepository<TDocument>.DeleteItemIfMatch(documentId, partitionKey, expectedETag, cancellationToken)` | Deletes an item only when its current ETag matches `expectedETag`. | Applies the operation only to documents whose current ETag matches the supplied expected value. |
| `ICosmosRepository<TDocument>.DeleteAll(delayMs, useQueue, cancellationToken)` | Deletes all items. | A task representing the asynchronous operation. |
| `ICosmosRepository<TDocument>.DeleteIdsIfMatch(ids, expectedETags, delayMs, cancellationToken)` | Deletes every item only when its current ETag matches the value keyed by its full ID. | Applies the operation only to documents whose current ETag matches the supplied expected value. |
| `ICosmosRepository<TDocument>.DeleteIdsParallelIfMatch(ids, expectedETags, maxConcurrency, cancellationToken)` | Deletes every item in parallel only when its current ETag matches the value keyed by its full ID. | Applies the operation only to documents whose current ETag matches the supplied expected value. |
| `ICosmosRepository<TDocument>.Exists(id, cancellationToken)` | Checks whether the repository contains the fully qualified document ID. | Returns `true` when the document exists; otherwise, `false`. |
| `ICosmosRepository<TDocument>.Exists(documentId, partitionKey, cancellationToken)` | Checks whether the specified partition contains the document ID. | Returns `true` when the document exists; otherwise, `false`. |
| `ICosmosRepository<TDocument>.Exists(query, cancellationToken)` | Checks whether the query matches at least one document. | Returns `true` when a match exists; otherwise, `false`. |
| `ICosmosRepository<TDocument>.ExistsByPartitionKey(partitionKey, cancellationToken)` | Checks for by Partition Key. | Returns `true` when at least one matching document exists; otherwise, `false`. |
| `ICosmosRepository<TDocument>.GetItemWithETag(id, cancellationToken)` | Gets an item together with the ETag required for a subsequent conditional write. | Applies the operation only to documents whose current ETag matches the supplied expected value. |
| `ICosmosRepository<TDocument>.GetItemWithETag(documentId, partitionKey, cancellationToken)` | Gets an item together with the ETag required for a subsequent conditional write. | Applies the operation only to documents whose current ETag matches the supplied expected value. |
| `ICosmosRepository<TDocument>.GetItem(id, cancellationToken)` | Get one item by Id (partition id and document id, or one guid if they're the same) Will not throw. | null if cannot be found. |

## Important behavior

- `ICosmosRepository<TDocument>.MutateItem(id, mutation, cancellationToken, maxAttempts)`: The mutation may be invoked more than once and must only describe the intended document delta. It must not perform non-idempotent external side effects.
- `ICosmosRepository<TDocument>.ExecuteOnGetItemsPaged(query, resultTask, cancellationToken)`: Include an `ORDER BY` clause when continuation-token paging must be stable.
- `ICosmosRepository<TDocument>.GetItemsPaged(queryDefinition, pageSize, continuationToken, cancellationToken)`: Include an `ORDER BY` clause; without deterministic ordering, continuation tokens may not resume reliably.
- `ICosmosRepository<TDocument>.GetItemsPaged(query, cancellationToken)`: Include an `ORDER BY` clause when consuming multiple pages.

## Practical notes

- Cancellation stops pending work; it does not undo work that has already completed.

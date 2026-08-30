[![](https://img.shields.io/nuget/v/Soenneker.Cosmos.Repository.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Cosmos.Repository/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.cosmos.repository/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.cosmos.repository/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Cosmos.Repository.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Cosmos.Repository/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.cosmos.repository/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.cosmos.repository/actions/workflows/codeql.yml)

# Soenneker.Cosmos.Repository

An extensible Azure Cosmos DB repository base with point reads, queries, paging, queued and parallel writes, ETag concurrency, patching, bulk deletion, and audit records.

## Installation

```bash
dotnet add package Soenneker.Cosmos.Repository
```

## Define a repository

The package provides an abstract base rather than a registrar. Documents must derive from `Soenneker.Documents.Document` and provide the `DocumentId`, `PartitionKey`, and other base fields expected by the repository.

```csharp
public interface IOrderRepository : ICosmosRepository<OrderDocument>
{
}

public sealed class OrderRepository : CosmosRepository<OrderDocument>, IOrderRepository
{
    public override string ContainerName => "orders";

    public OrderRepository(
        ICosmosContainerUtil containerUtil,
        IConfiguration configuration,
        ILogger<CosmosRepository<OrderDocument>> logger,
        IUserContext userContext,
        IBackgroundQueue backgroundQueue,
        IMemoryStreamUtil memoryStreamUtil)
        : base(containerUtil, configuration, logger, userContext, backgroundQueue, memoryStreamUtil)
    {
    }
}
```

Register the derived repository as scoped when using the scoped user-context implementation:

```csharp
services.AddScoped<IOrderRepository, OrderRepository>();
```

Register the constructor dependencies separately. `Soenneker.Cosmos.Container` supplies the container utility and its `Azure:Cosmos` configuration contract.

## IDs and partition keys

Single-string overloads accept a full ID in `partitionKey:documentId` form. When the partition key and document ID are the same, one value is sufficient. Two-string overloads consistently take `documentId` first and `partitionKey` second.

```csharp
OrderDocument? order = await orders.GetItem("tenant-42:order-100", cancellationToken);

bool exists = await orders.Exists(
    documentId: "order-100",
    partitionKey: "tenant-42",
    cancellationToken: cancellationToken);
```

Override `ResolvePartitionKey` if a derived repository uses a different full-ID convention.

## Writes and optimistic concurrency

```csharp
string fullId = await orders.AddItem(order, cancellationToken: cancellationToken);

CosmosItem<OrderDocument>? current = await orders.GetItemWithETag(fullId, cancellationToken);
if (current is not null)
{
    current.Document.Status = "shipped";
    CosmosItem<OrderDocument> updated = await orders.UpdateItemIfMatch(current, cancellationToken);
}
```

Conditional update, patch, and delete methods send the supplied ETag through Cosmos `If-Match`. A concurrent change results in the Cosmos 412 Precondition Failed error. `MutateItem` wraps this pattern with retries; its mutation delegate can run more than once and must not perform external side effects.

`AddItem` uses create semantics and fails when the addressed item already exists. Updates use replace semantics. Patch operations are sent directly to Cosmos.

## Queued and parallel work

Methods with `useQueue: true` return after the work is accepted by the configured background queue, not after Cosmos completes it. A queued patch returns `null`, and response-excluding writes return the caller's document because Cosmos does not send the updated resource body. Observe the background queue for execution failures and drain it during graceful shutdown.

Parallel methods perform direct Cosmos operations with bounded concurrency. Failures propagate to the caller; successful operations completed before a failure are not rolled back.

## Queries and paging

Prefer `QueryDefinition` with parameters for dynamic values:

```csharp
var query = new QueryDefinition(
        "SELECT * FROM c WHERE c.partitionKey = @partitionKey ORDER BY c.createdAt DESC")
    .WithParameter("@partitionKey", "tenant-42");

(List<OrderDocument> page, string? next) = await orders.GetItemsPaged(
    query,
    pageSize: 50,
    continuationToken: null,
    cancellationToken: cancellationToken);
```

Pass the returned continuation token unchanged to request the next page. Use a deterministic `ORDER BY` when results must remain stable between pages. Collection-returning query methods drain every page into memory; use paged or callback-based methods for large result sets.

`GetItem`, `GetItemWithETag`, and delete operations treat 404 Not Found as an absent or already-deleted item where documented. Authentication, throttling, service, query, and cancellation failures propagate rather than being reported as “not found.”

## Auditing

`AuditEnabled` defaults to `true`. After successful creates, updates, patches, and actual deletes, the repository queues an `AuditDocument` in the `audits` container using the target document ID as its audit partition key. Override `AuditEnabled` to disable this for a repository, including the audit repository itself.

Primary writes and audit writes are not transactional. A primary write can succeed before audit enqueueing or execution fails. Queued write auditing captures the serialized document at enqueue time.

## Logging

```json
{
  "Azure": {
    "Cosmos": {
      "Log": false,
      "AuditLog": false
    }
  }
}
```

These optional flags enable diagnostic operation and audit metadata logs. Document bodies, audit payloads, query parameter values, and continuation tokens are not logged.

## Deletion behavior

Delete-all and query-delete operations are permanent and non-transactional across the complete result set. Transactional batch deletion groups documents by partition key, limits each Cosmos batch to 100 operations, and now fails the call when Cosmos rejects a batch. Time-range deletion expects a queryable `createdAt` field.

Queued deletion means “enqueued,” while direct and parallel deletion means Cosmos acknowledged the operation. Cancellation or failure does not undo completed or queued deletes.

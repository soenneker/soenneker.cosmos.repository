﻿using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Documents.Document;
using Soenneker.Enums.CrudEventTypes;
using Soenneker.Enums.JsonOptions;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Json;
using Soenneker.Utils.Method;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public virtual ValueTask<string> AddItem(TDocument document, bool useQueue = false, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        if (_log)
        {
            string? serialized = JsonUtil.Serialize(document, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {document}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        return InternalAddItem(document, useQueue, excludeResponse, cancellationToken);
    }

    private async ValueTask<string> InternalAddItem(TDocument document, bool useQueue, bool excludeResponse, CancellationToken cancellationToken)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

        return await InternalAddItemWithContainer(document, container, useQueue, excludeResponse, cancellationToken);
    }

    private async ValueTask<string> InternalAddItemWithContainer(TDocument document, Microsoft.Azure.Cosmos.Container container, bool useQueue,
        bool excludeResponse, CancellationToken cancellationToken)
    {
        if (document.PartitionKey.IsNullOrWhiteSpace() || document.DocumentId.IsNullOrWhiteSpace())
            throw new Exception("DocumentId and PartitionKey MUST be present on the object before storing");

        ItemRequestOptions? options = null;

        if (excludeResponse)
            options = CosmosRequestOptions.ExcludeResponse;

        if (useQueue)
        {
            await _backgroundQueue
                  .QueueValueTask(
                      async token => { await container.CreateItemAsync(document, new PartitionKey(document.PartitionKey), options, token).NoSync(); },
                      cancellationToken)
                  .NoSync();
        }
        else
        {
            await container.CreateItemAsync(document, new PartitionKey(document.PartitionKey), options, cancellationToken).NoSync();
        }

        if (AuditEnabled)
        {
            await CreateAuditItem(CrudEventType.Create, document.Id, document, cancellationToken).NoSync();
        }

        return document.Id;
    }
}
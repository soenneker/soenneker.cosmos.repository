﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Enums.EventType;
using Soenneker.Enums.JsonOptions;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Json;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<TDocument> UpdateItem(TDocument item, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
       return UpdateItem(item.Id, item, useQueue, excludeResponse, cancellationToken);
    }

    public async ValueTask<List<TDocument>> UpdateItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
        TimeSpan? timespanDelay = null;

        if (delayMs != null)
            timespanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

        foreach (TDocument item in documents)
        {
            _ = await UpdateItem(item.Id, item, useQueue, excludeResponse, cancellationToken).NoSync();

            if (delayMs != null)
                await Task.Delay(timespanDelay!.Value, cancellationToken).NoSync();
        }

        return documents;
    }

    public async ValueTask<TDocument> UpdateItem(string id, TDocument item, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
        if (_log)
        {
            string? serialized = JsonUtil.Serialize(item, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        (string? partitionKey, string? documentId) = id.ToSplitId();

        TDocument? updatedDocument = null;

        ItemRequestOptions? options = null;

        if (excludeResponse)
            options = _excludeRequestOptions;

        // TODO: we should probably move this to replace
        if (useQueue)
        {
            await _backgroundQueue.QueueValueTask(async token =>
            {
                Microsoft.Azure.Cosmos.Container container = await Container(token).NoSync();

                ItemResponse<TDocument>? _ = await container.ReplaceItemAsync(item, documentId, new PartitionKey(partitionKey), options, token).NoSync();
                //Logger.LogInformation(response.RequestCharge.ToString());
            }, cancellationToken).NoSync();
        }
        else
        {
            Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();

            ItemResponse<TDocument>? response = await container.ReplaceItemAsync(item, documentId, new PartitionKey(partitionKey), options, cancellationToken).NoSync();
            //Logger.LogInformation(response.RequestCharge.ToString());
            updatedDocument = response.Resource;
        }

        if (AuditEnabled)
            await CreateAuditItem(EventType.Update, id, item, cancellationToken).NoSync();

        if (updatedDocument == null)
            return item;

        return updatedDocument;
    }
}
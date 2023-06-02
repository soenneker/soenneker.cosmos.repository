﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Documents.Document;
using Soenneker.Enums.EventType;
using Soenneker.Enums.JsonOptions;
using Soenneker.Extensions.String;
using Soenneker.Utils.Json;
using Soenneker.Utils.Method;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<TDocument> UpdateItem(TDocument item, bool useQueue = false, bool excludeResponse = false)
    {
       return UpdateItem(item.Id, item, useQueue, excludeResponse);
    }

    public async ValueTask<List<TDocument>> UpdateItems(List<TDocument> documents, double? delayMs = null, bool useQueue = false, bool excludeResponse = false)
    {
        TimeSpan? timespanDelay = null;

        if (delayMs != null)
            timespanDelay = TimeSpan.FromMilliseconds(delayMs.Value);

        foreach (TDocument item in documents)
        {
            _ = await UpdateItem(item.Id, item, useQueue, excludeResponse).ConfigureAwait(false);

            if (delayMs != null)
                await Task.Delay(timespanDelay!.Value).ConfigureAwait(false);
        }

        return documents;
    }

    public async ValueTask<TDocument> UpdateItem(string id, TDocument item, bool useQueue = false, bool excludeResponse = false)
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
            await _backgroundQueue.QueueValueTask(async _ =>
            {
                Microsoft.Azure.Cosmos.Container container = await Container.ConfigureAwait(false);

                ItemResponse<TDocument>? response = await container.ReplaceItemAsync(item, documentId, new PartitionKey(partitionKey), options, _).ConfigureAwait(false);
                //Logger.LogInformation(response.RequestCharge.ToString());
            });
        }
        else
        {
            Microsoft.Azure.Cosmos.Container container = await Container.ConfigureAwait(false);

            ItemResponse<TDocument>? response = await container.ReplaceItemAsync(item, documentId, new PartitionKey(partitionKey), options).ConfigureAwait(false);
            //Logger.LogInformation(response.RequestCharge.ToString());
            updatedDocument = response.Resource;
        }

        if (AuditEnabled)
            await CreateAuditItem(EventType.Update, id, item).ConfigureAwait(false);

        if (updatedDocument == null)
            return item;

        return updatedDocument;
    }
}
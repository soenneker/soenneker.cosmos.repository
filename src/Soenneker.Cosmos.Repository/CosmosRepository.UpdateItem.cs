using System.IO;
using System.Runtime.CompilerServices;
using System;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Soenneker.Cosmos.RequestOptions;
using Soenneker.Cosmos.Repository.Dtos;
using Soenneker.Documents.Document;
using Soenneker.Enums.CrudEventTypes;
using Soenneker.Enums.JsonOptions;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Json;
using Soenneker.Utils.Method;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Enums.JsonLibrary;

namespace Soenneker.Cosmos.Repository;

public abstract partial class CosmosRepository<TDocument> where TDocument : Document
{
    public ValueTask<CosmosItem<TDocument>> UpdateItemIfMatch(CosmosItem<TDocument> item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return UpdateItemIfMatch(GetRequiredId(item.Document), item.Document, item.ETag, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TDocument> UpdateItem(TDocument item, bool useQueue = false, bool excludeResponse = false, CancellationToken cancellationToken = default)
    {
        return UpdateItemCore(GetRequiredId(item), item, useQueue, excludeResponse, cancellationToken);
    }

    public ValueTask<CosmosItem<TDocument>> UpdateItemIfMatch(TDocument item, string expectedETag,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedETag);
        return UpdateItemIfMatchCore(GetRequiredId(item), item, expectedETag, cancellationToken);
    }

    public async ValueTask<TDocument> UpdateItem(string id, TDocument item, bool useQueue = false, bool excludeResponse = false,
        CancellationToken cancellationToken = default)
    {
        return await UpdateItemCore(id, item, useQueue, excludeResponse, cancellationToken).NoSync();
    }

    public ValueTask<CosmosItem<TDocument>> UpdateItemIfMatch(string id, TDocument item, string expectedETag,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedETag);
        return UpdateItemIfMatchCore(id, item, expectedETag, cancellationToken);
    }

    private async ValueTask<TDocument> UpdateItemCore(string id, TDocument item, bool useQueue, bool excludeResponse,
        CancellationToken cancellationToken)
    {
        bool auditEnabled = AuditEnabled;

        if (_log && Logger.IsEnabled(LogLevel.Debug))
        {
            string? serialized = JsonUtil.Serialize(item, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        (string partitionKey, string documentId) = id.ToSplitId();

        PartitionKey pk = new(partitionKey);
        ItemRequestOptions? options = excludeResponse ? CosmosRequestOptions.ExcludeResponse : null;

        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken)
            .NoSync();

        if (useQueue)
        {
            string? itemJson = JsonUtil.Serialize(item, JsonOptionType.Web, JsonLibraryType.SystemTextJson);

            await _backgroundQueue.QueueValueTask(
                                      (Container: container, DocumentId: documentId, PartitionKey: pk, Json: itemJson, Options: options,
                                          MemoryStreamUtil: _memoryStreamUtil, AuditEnabled: auditEnabled, FullId: id, Self: this), static async (s, token) =>
                                      {
                                          using MemoryStream ms = await s.MemoryStreamUtil.Get(s.Json, token)
                                                                         .NoSync();

                                          using ResponseMessage resp = await s
                                                                             .Container.ReplaceItemStreamAsync(ms, s.DocumentId, s.PartitionKey, s.Options,
                                                                                 token)
                                                                             .NoSync();

                                          resp.EnsureSuccessStatusCode();

                                          if (s.AuditEnabled)
                                          {
                                              await s.Self.CreateAuditItem(CrudEventType.Update, s.FullId, s.Json, token)
                                                     .NoSync();
                                          }
                                      }, cancellationToken)
                                  .NoSync();

            return item;
        }

        ItemResponse<TDocument> response = await container.ReplaceItemAsync(item, documentId, pk, options, cancellationToken)
                                                          .NoSync();

        if (auditEnabled)
        {
            await CreateAuditItem(CrudEventType.Update, id, item, cancellationToken)
                .NoSync();
        }

        return response.Resource ?? item;
    }

    private async ValueTask<CosmosItem<TDocument>> UpdateItemIfMatchCore(string id, TDocument item, string expectedETag,
        CancellationToken cancellationToken)
    {
        Microsoft.Azure.Cosmos.Container container = await Container(cancellationToken).NoSync();
        return await UpdateItemIfMatchWithContainer(container, id, item, expectedETag, cancellationToken).NoSync();
    }

    private async ValueTask<CosmosItem<TDocument>> UpdateItemIfMatchWithContainer(Microsoft.Azure.Cosmos.Container container, string id,
        TDocument item, string expectedETag, CancellationToken cancellationToken)
    {
        if (_log && Logger.IsEnabled(LogLevel.Debug))
        {
            string? serialized = JsonUtil.Serialize(item, JsonOptionType.Pretty);
            Logger.LogDebug("-- COSMOS: {method} ({type}): {item}", MethodUtil.Get(), typeof(TDocument).Name, serialized);
        }

        (string partitionKey, string documentId) = id.ToSplitId();
        var options = new ItemRequestOptions {IfMatchEtag = expectedETag};

        ItemResponse<TDocument> response = await container
                                                 .ReplaceItemAsync(item, documentId, new PartitionKey(partitionKey), options, cancellationToken)
                                                 .NoSync();

        if (AuditEnabled)
            await CreateAuditItem(CrudEventType.Update, id, item, cancellationToken).NoSync();

        return new CosmosItem<TDocument>(response.Resource ?? item, response.ETag);
    }

    private static string GetRequiredId(TDocument document)
    {
        string? id = document.Id;
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return id;
    }
}

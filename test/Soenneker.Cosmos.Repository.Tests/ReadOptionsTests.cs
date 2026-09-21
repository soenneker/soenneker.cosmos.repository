using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Soenneker.Cosmos.Repository.Abstract;
using Soenneker.Cosmos.Repository.Dtos;
using Soenneker.Dtos.IdNamePair;
using Soenneker.Dtos.IdPartitionPair;

namespace Soenneker.Cosmos.Repository.Tests;

public partial class PerformanceRegressionTests
{
    private static CosmosReadOptions? ReadOptions(int mode) => mode == 1 ? new CosmosReadOptions
    {
        ReadConsistencyStrategy = ReadConsistencyStrategy.LatestCommitted,
        SessionToken = "0:42"
    } : mode == 2 ? new CosmosReadOptions() : null;

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task PointReadsForwardOptionalConsistencyAndCancellation(int mode)
    {
        CosmosReadOptions? options = ReadOptions(mode);
        using var cts = new CancellationTokenSource();
        var document = new TestDocument { DocumentId = "doc", PartitionKey = "pk" };
        var response = new Mock<ItemResponse<TestDocument>>();
        response.SetupGet(r => r.Resource).Returns(document);
        response.SetupGet(r => r.ETag).Returns("etag");
        var seen = new List<ItemRequestOptions?>();
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.ReadItemAsync<TestDocument>("doc", new PartitionKey("pk"), It.IsAny<ItemRequestOptions>(), cts.Token))
            .Callback<string, PartitionKey, ItemRequestOptions, CancellationToken>((_, _, request, _) => seen.Add(request))
            .ReturnsAsync(response.Object);
        container.Setup(c => c.ReadItemStreamAsync("doc", new PartitionKey("pk"), It.IsAny<ItemRequestOptions>(), cts.Token))
            .Callback<string, PartitionKey, ItemRequestOptions, CancellationToken>((_, _, request, _) => seen.Add(request))
            .Returns(() => Task.FromResult(new ResponseMessage(HttpStatusCode.OK)));
        ICosmosRepository<TestDocument> repo = CreateRepository(container.Object);

        (await repo.GetItem("pk:doc", options, cancellationToken: cts.Token)).Should().BeSameAs(document);
        (await repo.GetItem("doc", "pk", options, cancellationToken: cts.Token)).Should().BeSameAs(document);
        (await repo.GetItemWithETag("pk:doc", options, cancellationToken: cts.Token))!.ETag.Should().Be("etag");
        (await repo.GetItemWithETag("doc", "pk", options, cancellationToken: cts.Token))!.Document.Should().BeSameAs(document);
        (await repo.Exists("pk:doc", options, cancellationToken: cts.Token)).Should().BeTrue();
        (await repo.Exists("doc", "pk", options, cancellationToken: cts.Token)).Should().BeTrue();

        seen.Count.Should().Be(6);
        if (mode == 1)
            seen[0].Should().NotBeSameAs(seen[1]);
        foreach (ItemRequestOptions? request in seen)
        {
            if (mode != 1)
                request.Should().BeNull();
            else
            {
                request!.ConsistencyLevel.Should().BeNull();
                request.ReadConsistencyStrategy.Should().Be(options?.ReadConsistencyStrategy);
                request.SessionToken.Should().Be(options?.SessionToken);
            }
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task QueryReadsForwardOptionsAcrossWrappersAndIdBatches(int mode)
    {
        CosmosReadOptions? options = ReadOptions(mode);
        var requests = new List<QueryRequestOptions?>();
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        CaptureQueryOptions<TestDocument>(container, requests);
        CaptureQueryOptions<string>(container, requests);
        CaptureQueryOptions<IdPartitionPair>(container, requests);
        ICosmosRepository<TestDocument> repo = CreateRepository(container.Object);

        await repo.GetAll(readOptions: options);
        await repo.GetItems("SELECT * FROM c", readOptions: options);
        await repo.GetItems<TestDocument>("SELECT * FROM c", readOptions: options);
        await repo.GetItems(new QueryDefinition("SELECT * FROM c"), readOptions: options);
        await repo.GetItems<TestDocument>(new QueryDefinition("SELECT * FROM c"), readOptions: options);
        await repo.GetAllByDocumentIds(Enumerable.Range(0, 51).Select(i => i.ToString()).ToList(), readOptions: options);
        await repo.GetItemsBetween(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, readOptions: options);
        await repo.GetAllIds(readOptions: options);
        await repo.GetAllPartitionKeys(readOptions: options);

        requests.Count.Should().Be(10);
        if (mode == 1)
            requests[5].Should().BeSameAs(requests[6]);
        foreach (QueryRequestOptions? request in requests)
        {
            if (mode != 1)
                request.Should().BeNull();
            else
                AssertQueryReadOptions(request!, options);
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task PartitionReadsRetainRoutingAndSingleItemSettings(int mode)
    {
        CosmosReadOptions? options = ReadOptions(mode);
        var requests = new List<QueryRequestOptions?>();
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        CaptureQueryOptions<TestDocument>(container, requests);
        CaptureQueryOptions<int>(container, requests);
        var repo = CreateRepository(container.Object);

        await repo.GetItemByPartitionKey("pk", readOptions: options);
        await repo.GetLatestByPartitionKey("pk", readOptions: options);
        await repo.ExistsByPartitionKey("pk", readOptions: options);
        await repo.GetAllByPartitionKey("pk", readOptions: options);

        requests.Count.Should().Be(4);
        foreach (QueryRequestOptions? request in requests)
        {
            AssertQueryReadOptions(request!, options);
            request!.PartitionKey.Should().Be(new PartitionKey("pk"));
        }
        foreach (QueryRequestOptions? request in requests.Take(3))
        {
            request!.MaxItemCount.Should().Be(1);
            request.EnableOptimisticDirectExecution.Should().BeTrue();
        }
        requests[3]!.MaxItemCount.Should().BeNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task ReadManyForwardsOptionalReadSettings(int mode)
    {
        CosmosReadOptions? options = ReadOptions(mode);
        var requests = new List<ReadManyRequestOptions?>();
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.ReadManyItemsAsync<TestDocument>(It.IsAny<IReadOnlyList<(string, PartitionKey)>>(),
                It.IsAny<ReadManyRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<(string, PartitionKey)>, ReadManyRequestOptions, CancellationToken>((_, request, _) => requests.Add(request))
            .ReturnsAsync(new TestResponse<TestDocument>([]));
        var repo = CreateRepository(container.Object);

        await repo.GetAllByIdNamePairs([new IdNamePair { Id = "doc", Name = "name" }], readOptions: options);
        await repo.GetAllByIdPartitionPairs([new IdPartitionPair { Id = "doc", PartitionKey = "pk" }], readOptions: options);

        requests.Count.Should().Be(2);
        foreach (ReadManyRequestOptions? request in requests)
        {
            if (mode != 1)
                request.Should().BeNull();
            else
            {
                request!.ConsistencyLevel.Should().BeNull();
                request.ReadConsistencyStrategy.Should().Be(options?.ReadConsistencyStrategy);
                request.SessionToken.Should().Be(options?.SessionToken);
            }
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task PagedExecutionCarriesReadOptionsToEveryPage(int mode)
    {
        CosmosReadOptions? options = ReadOptions(mode);
        var requests = new List<QueryRequestOptions>();
        var continuations = new List<string?>();
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.GetItemQueryIterator<TestDocument>(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Returns((QueryDefinition _, string? continuation, QueryRequestOptions request) =>
            {
                requests.Add(request);
                continuations.Add(continuation);
                var page = new Mock<FeedResponse<TestDocument>>();
                page.SetupGet(p => p.Count).Returns(0);
                page.SetupGet(p => p.ContinuationToken).Returns(continuation is null ? "next" : null!);
                var iterator = new Mock<FeedIterator<TestDocument>>();
                iterator.SetupGet(i => i.HasMoreResults).Returns(true);
                iterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(page.Object);
                return iterator.Object;
            });
        var repo = CreateRepository(container.Object);
        var callbacks = 0;

        await repo.ExecuteOnGetItemsPaged(new QueryDefinition("SELECT * FROM c"), 25, _ =>
        {
            callbacks++;
            return ValueTask.CompletedTask;
        }, readOptions: options);

        callbacks.Should().Be(2);
        continuations.Should().Equal(new string?[] { null, "next" });
        foreach (QueryRequestOptions request in requests)
        {
            AssertQueryReadOptions(request, options);
            request.MaxItemCount.Should().Be(25);
        }
        requests[0].Should().NotBeSameAs(requests[1]);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task PagedLinqBuilderPreservesReadOptionsAndPaging(int mode)
    {
        CosmosReadOptions? options = ReadOptions(mode);
        var requests = new List<QueryRequestOptions>();
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.GetItemLinqQueryable<TestDocument>(false, "resume", It.IsAny<QueryRequestOptions>(), null))
            .Callback<bool, string, QueryRequestOptions, CosmosLinqSerializerOptions>((_, _, request, _) => requests.Add(request))
            .Returns(Array.Empty<TestDocument>().AsQueryable().OrderBy(d => d.Id));
        var repo = CreateRepository(container.Object);

        await repo.BuildPagedQueryable(25, "resume", readOptions: options);
        await repo.BuildPagedQueryable<TestDocument>(25, "resume", readOptions: options);

        requests.Count.Should().Be(2);
        foreach (QueryRequestOptions request in requests)
        {
            AssertQueryReadOptions(request, options);
            request.MaxItemCount.Should().Be(25);
        }
    }

    [Test]
    public async Task MutationRetriesKeepReadOptionsAndETagProtection()
    {
        CosmosReadOptions options = ReadOptions(1)!.Value;
        var readRequests = new List<ItemRequestOptions>();
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        var read = new Mock<ItemResponse<TestDocument>>();
        read.SetupGet(r => r.Resource).Returns(() => new TestDocument { DocumentId = "doc", PartitionKey = "pk" });
        read.SetupGet(r => r.ETag).Returns("etag");
        container.Setup(c => c.ReadItemAsync<TestDocument>("doc", new PartitionKey("pk"),
                It.Is<ItemRequestOptions>(o => o.ReadConsistencyStrategy == options.ReadConsistencyStrategy &&
                                              o.ConsistencyLevel == null && o.SessionToken == options.SessionToken),
                It.IsAny<CancellationToken>()))
            .Callback<string, PartitionKey, ItemRequestOptions, CancellationToken>((_, _, request, _) => readRequests.Add(request))
            .ReturnsAsync(read.Object);
        container.SetupSequence(c => c.ReplaceItemAsync(It.IsAny<TestDocument>(), "doc", new PartitionKey("pk"),
                It.Is<ItemRequestOptions>(o => o.IfMatchEtag == "etag"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Conflict", HttpStatusCode.PreconditionFailed, 0, "activity", 0))
            .ReturnsAsync(read.Object);
        var mutations = 0;

        await CreateRepository(container.Object, options, new CosmosWriteOptions { RequireETag = true }).MutateItem("pk:doc", document =>
        {
            mutations++;
            document.Updated = true;
            return true;
        });

        mutations.Should().Be(2);
        readRequests.Count.Should().Be(2);
        readRequests[0].Should().BeSameAs(readRequests[1]);
        container.Verify(c => c.ReadItemAsync<TestDocument>("doc", new PartitionKey("pk"), It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static void CaptureQueryOptions<T>(Mock<Microsoft.Azure.Cosmos.Container> container, List<QueryRequestOptions?> requests)
    {
        container.Setup(c => c.GetItemQueryIterator<T>(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((_, _, options) => requests.Add(options))
            .Returns(() => new TestIterator<T>([[]]));
    }

    private static void AssertQueryReadOptions(QueryRequestOptions request, CosmosReadOptions? expected)
    {
        request.ConsistencyLevel.Should().BeNull();
        request.ReadConsistencyStrategy.Should().Be(expected?.ReadConsistencyStrategy);
        request.SessionToken.Should().Be(expected?.SessionToken);
    }
}

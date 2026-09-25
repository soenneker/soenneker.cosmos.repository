using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Soenneker.Cosmos.Container.Abstract;
using Soenneker.Cosmos.Repository.Dtos;
using Soenneker.Documents.Document;
using Soenneker.Dtos.IdNamePair;
using Soenneker.Utils.BackgroundQueue.Abstract;
using Soenneker.Utils.MemoryStream.Abstract;
using Soenneker.Utils.UserContext.Abstract;

namespace Soenneker.Cosmos.Repository.Tests;

public partial class PerformanceRegressionTests
{
    [Test]
    public async Task ParallelAddsProcessEveryDocumentOnce()
    {
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        var seen = new int[32];
        container.Setup(c => c.CreateItemAsync(It.IsAny<TestDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Returns(async (TestDocument doc, PartitionKey? pk, ItemRequestOptions options, CancellationToken ct) =>
            {
                pk.Should().Be(new PartitionKey(doc.PartitionKey));
                Interlocked.Increment(ref seen[int.Parse(doc.DocumentId!)]);
                await Task.Yield();
                return Mock.Of<ItemResponse<TestDocument>>();
            });
        var documents = Enumerable.Range(0, seen.Length).Select(i => new TestDocument { DocumentId = i.ToString(), PartitionKey = "pk" }).ToList();
        (await CreateRepository(container.Object).AddItemsParallel(documents, 4)).Should().BeSameAs(documents);
        seen.Should().OnlyContain(count => count == 1);
        for (var i = 0; i < documents.Count; i++)
            documents[i].Id.Should().Be("pk:" + i);
    }

    [Test]
    public async Task EmptyBatchesDoNotResolveAContainer()
    {
        var util = new Mock<ICosmosContainerUtil>(MockBehavior.Strict);
        var repo = new TestRepository(util.Object);
        List<TestDocument> documents = [];
        (await repo.AddItems(documents)).Should().BeSameAs(documents);
        (await repo.AddItemsParallel(documents, 4)).Should().BeSameAs(documents);
        (await repo.UpdateItems(documents)).Should().BeSameAs(documents);
        (await repo.UpdateItemsParallel(documents, 4)).Should().BeSameAs(documents);
        (await repo.PatchItems(documents, [])).Should().BeSameAs(documents);
        await repo.DeleteIds([]);
        await repo.DeleteIdsParallel([], 4);
        Func<Task> invalid = async () => await repo.AddItemsParallel(documents, 0);
        await invalid.Should().ThrowAsync<ArgumentOutOfRangeException>();
        util.VerifyNoOtherCalls();
    }

    [Test]
    public async Task PatchBatchResolvesContainerOnce()
    {
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.PatchItemAsync<TestDocument>(It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<IReadOnlyList<PatchOperation>>(), It.IsAny<PatchItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ItemResponse<TestDocument>>());
        var util = new Mock<ICosmosContainerUtil>();
        util.Setup(u => u.Get("test", It.IsAny<CancellationToken>())).Returns(new ValueTask<Microsoft.Azure.Cosmos.Container>(container.Object));
        var documents = Enumerable.Range(0, 5).Select(i => new TestDocument { DocumentId = i.ToString(), PartitionKey = "pk" }).ToList();
        (await new TestRepository(util.Object).PatchItems(documents, [PatchOperation.Set("/updated", true)])).Should().BeSameAs(documents);
        util.Verify(u => u.Get("test", It.IsAny<CancellationToken>()), Times.Once);
        container.Verify(c => c.PatchItemAsync<TestDocument>(It.IsAny<string>(), It.IsAny<PartitionKey>(),
            It.IsAny<IReadOnlyList<PatchOperation>>(), It.IsAny<PatchItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SingleItemReadsSkipEmptyPages(bool latest)
    {
        var expected = new TestDocument { DocumentId = "doc", PartitionKey = "pk" };
        var iterator = new TestIterator<TestDocument>([[], [], [expected]]);
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.GetItemQueryIterator<TestDocument>(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Returns(iterator);
        var repo = CreateRepository(container.Object);
        TestDocument? result = latest ? await repo.GetLatestByPartitionKey("pk") : await repo.GetItemByPartitionKey("pk");
        result.Should().BeSameAs(expected);
        iterator.ReadCount.Should().Be(3);
        iterator.Disposed.Should().BeTrue();
    }

    [Test]
    public async Task ExistsSkipsEmptyPagesAndStopsAtFirstResult()
    {
        var iterator = new TestIterator<int>([[], [1], [2]]);
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.GetItemQueryIterator<int>(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
            .Returns(iterator);
        (await CreateRepository(container.Object).ExistsByPartitionKey("pk")).Should().BeTrue();
        iterator.ReadCount.Should().Be(2);
        iterator.Disposed.Should().BeTrue();
    }

    [Test]
    public async Task PagedLinqRewritesCallerNullFiltersBeforeCreatingQueryDefinition()
    {
        using var client = new CosmosClient("https://localhost:8081", Convert.ToBase64String(new byte[64]));
        IQueryable<TestDocument> query = client.GetContainer("test", "test").GetItemLinqQueryable<TestDocument>();
        query = query.Where(d => d.DocumentId == null);
        string expectedSql = client.GetContainer("test", "test").GetItemLinqQueryable<TestDocument>()
            .Where(d => !d.DocumentId.IsDefined() || d.DocumentId.IsNull()).ToQueryDefinition().QueryText;
        var iterator = new TestIterator<TestDocument>([[]]);
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.GetItemQueryIterator<TestDocument>(It.IsAny<QueryDefinition>(), "resume", It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((definition, _, options) =>
            {
                definition.QueryText.Should().Be(expectedSql);
                options.MaxItemCount.Should().Be(25);
            }).Returns(iterator);

        var result = await CreateRepository(container.Object).GetItemsPaged(query, 25, "resume");

        result.items.Should().BeEmpty();
        iterator.ReadCount.Should().Be(1);
        iterator.Disposed.Should().BeTrue();
        container.Verify(c => c.GetItemQueryIterator<TestDocument>(It.IsAny<QueryDefinition>(), "resume", It.IsAny<QueryRequestOptions>()), Times.Once);
    }

    [Test]
    public void ExistsProjectionTranslatesToConstantSql()
    {
        using var client = new CosmosClient("https://localhost:8081", Convert.ToBase64String(new byte[64]));
        IQueryable<TestDocument> query = client.GetContainer("test", "test").GetItemLinqQueryable<TestDocument>();
        string sql = query.Where(d => d.DocumentId == "doc").Select(static _ => 1).Take(1).ToQueryDefinition().QueryText;
        sql.Should().Contain("VALUE 1").And.Contain("TOP 1");
    }

    [Test]
    public async Task ReadManyUsesIdAsPartitionAndReusesResponseList()
    {
        var documents = new List<TestDocument> { new() { DocumentId = "a", PartitionKey = "a" } };
        var response = new TestResponse<TestDocument>(documents);
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.ReadManyItemsAsync<TestDocument>(It.IsAny<IReadOnlyList<(string, PartitionKey)>>(), It.IsAny<ReadManyRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<(string, PartitionKey)>, ReadManyRequestOptions, CancellationToken>((pairs, _, _) =>
            {
                pairs.Count.Should().Be(2);
                pairs[0].Should().Be(("a", new PartitionKey("a")));
                pairs[1].Should().Be(("b", new PartitionKey("b")));
            }).ReturnsAsync(response);
        var result = await CreateRepository(container.Object).GetAllByIdNamePairs([new IdNamePair { Id = "a", Name = "A" }, new IdNamePair { Id = "b", Name = "B" }]);
        result.Should().BeSameAs(documents);
    }

    [Test]
    public async Task ParallelUpdatesKeepIndicesAndBoundConcurrency()
    {
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        int active = 0, peak = 0;
        container.Setup(c => c.ReplaceItemAsync(It.IsAny<TestDocument>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Returns(async (TestDocument doc, string id, PartitionKey? pk, ItemRequestOptions options, CancellationToken ct) =>
            {
                int current = Interlocked.Increment(ref active);
                int previous;
                do { previous = Volatile.Read(ref peak); } while (current > previous && Interlocked.CompareExchange(ref peak, current, previous) != previous);
                await Task.Delay(2, ct);
                Interlocked.Decrement(ref active);
                var response = new Mock<ItemResponse<TestDocument>>();
                response.SetupGet(r => r.Resource).Returns(new TestDocument { DocumentId = id, PartitionKey = "pk", Updated = true });
                response.SetupGet(r => r.ETag).Returns("new-etag");
                return response.Object;
            });
        var documents = Enumerable.Range(0, 32).Select(i => new TestDocument { DocumentId = i.ToString(), PartitionKey = "pk" }).ToList();
        var repo = CreateRepository(container.Object);
        (await repo.UpdateItemsParallel(documents, 4)).Should().BeSameAs(documents);
        peak.Should().BeInRange(2, 4);
        for (var i = 0; i < documents.Count; i++)
        {
            documents[i].DocumentId.Should().Be(i.ToString());
            documents[i].Updated.Should().BeTrue();
        }
        var conditional = documents.Select(d => new CosmosItem<TestDocument>(d, "old-etag")).ToList();
        (await repo.UpdateItemsParallelIfMatch(conditional, 4)).Should().BeSameAs(conditional);
        conditional.Should().OnlyContain(item => item.ETag == "new-etag");
    }

    private static TestRepository CreateRepository(Microsoft.Azure.Cosmos.Container container,
        CosmosReadOptions? readOptions = null, CosmosWriteOptions? writeOptions = null)
    {
        var util = new Mock<ICosmosContainerUtil>();
        util.Setup(u => u.Get("test", It.IsAny<CancellationToken>())).Returns(new ValueTask<Microsoft.Azure.Cosmos.Container>(container));
        return new TestRepository(util.Object, readOptions, writeOptions);
    }

    public sealed class TestDocument : Document { public bool Updated { get; set; } }
    private sealed class TestRepository(ICosmosContainerUtil util, CosmosReadOptions? readOptions = null,
        CosmosWriteOptions? writeOptions = null) : CosmosRepository<TestDocument>(TestJsonContext.Default, util,
        new ConfigurationBuilder().Build(), NullLogger<CosmosRepository<TestDocument>>.Instance,
        Mock.Of<IUserContext>(), Mock.Of<IBackgroundQueue>(), Mock.Of<IMemoryStreamUtil>())
    {
        public override string ContainerName => "test";
        public override bool AuditEnabled => false;
        public override CosmosReadOptions? DefaultReadOptions => readOptions;
        public override CosmosWriteOptions? DefaultWriteOptions => writeOptions;
    }

    private sealed class TestIterator<T>(IReadOnlyList<IReadOnlyList<T>> pages) : FeedIterator<T>
    {
        public int ReadCount { get; private set; }
        public bool Disposed { get; private set; }
        public override bool HasMoreResults => ReadCount < pages.Count;
        public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<FeedResponse<T>>(new TestResponse<T>(pages[ReadCount++]));
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TestResponse<T>(IReadOnlyList<T> items) : FeedResponse<T>
    {
        public override string ContinuationToken => null!;
        public override int Count => items.Count;
        public override string IndexMetrics => null!;
        public override Headers Headers => null!;
        public override IEnumerable<T> Resource => items;
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;
        public override CosmosDiagnostics Diagnostics => null!;
        public override IEnumerator<T> GetEnumerator() => items.GetEnumerator();
    }
}

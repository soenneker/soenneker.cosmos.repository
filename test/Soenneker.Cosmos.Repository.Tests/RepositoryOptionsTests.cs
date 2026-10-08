using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Soenneker.Cosmos.Container.Abstract;
using Soenneker.Cosmos.Repository.Dtos;
using Soenneker.Dtos.IdPartitionPair;

namespace Soenneker.Cosmos.Repository.Tests;

public partial class PerformanceRegressionTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async ValueTask RepositoryReadDefaultsCanBeOverriddenOrCleared(int mode, CancellationToken cancellationToken)
    {
        var defaults = new CosmosReadOptions { ReadConsistencyStrategy = ReadConsistencyStrategy.LatestCommitted, SessionToken = "0:42" };
        CosmosReadOptions? methodOptions = mode switch
        {
            1 => new CosmosReadOptions { ReadConsistencyStrategy = ReadConsistencyStrategy.Eventual },
            2 => new CosmosReadOptions(),
            _ => null
        };
        CosmosReadOptions expected = methodOptions ?? defaults;
        var queries = new List<QueryRequestOptions?>();
        var items = new List<ItemRequestOptions?>();
        var many = new List<ReadManyRequestOptions?>();
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        CaptureQueryOptions<TestDocument>(container, queries);
        CaptureQueryOptions<IdPartitionPair>(container, queries);
        var response = new Mock<ItemResponse<TestDocument>>();
        response.SetupGet(r => r.Resource).Returns(new TestDocument { DocumentId = "doc", PartitionKey = "pk" });
        response.SetupGet(r => r.ETag).Returns("etag");
        container.Setup(c => c.ReadItemAsync<TestDocument>("doc", new PartitionKey("pk"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, PartitionKey, ItemRequestOptions, CancellationToken>((_, _, request, _) => items.Add(request))
            .ReturnsAsync(response.Object);
        container.Setup(c => c.ReadManyItemsAsync<TestDocument>(It.IsAny<IReadOnlyList<(string, PartitionKey)>>(),
                It.IsAny<ReadManyRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<(string, PartitionKey)>, ReadManyRequestOptions, CancellationToken>((_, request, _) => many.Add(request))
            .ReturnsAsync(new TestResponse<TestDocument>([]));
        var repo = CreateRepository(container.Object, defaults, new CosmosWriteOptions { RequireETag = true });

        await repo.GetItem("pk:doc", readOptions: methodOptions, cancellationToken: cancellationToken);
        await repo.GetItemWithETag("pk:doc", readOptions: methodOptions, cancellationToken: cancellationToken);
        await repo.MutateItem("pk:doc", _ => false, readOptions: methodOptions, cancellationToken: cancellationToken);
        await repo.GetAll(readOptions: methodOptions, cancellationToken: cancellationToken);
        await repo.GetAllIds(readOptions: methodOptions, cancellationToken: cancellationToken);
        await repo.GetItemsPaged(new QueryDefinition("SELECT * FROM c"), 25, null, readOptions: methodOptions, cancellationToken: cancellationToken);
        await repo.GetAllByPartitionKey("pk", readOptions: methodOptions, cancellationToken: cancellationToken);
        await repo.GetAllByIdPartitionPairs([new() { Id = "doc", PartitionKey = "pk" }], readOptions: methodOptions, cancellationToken: cancellationToken);

        items.Count.Should().Be(3);
        queries.Count.Should().Be(4);
        foreach (ItemRequestOptions? request in items)
        {
            (request?.ReadConsistencyStrategy).Should().Be(expected.ReadConsistencyStrategy);
            (request?.SessionToken).Should().Be(expected.SessionToken);
        }
        foreach (QueryRequestOptions? request in queries)
        {
            (request?.ReadConsistencyStrategy).Should().Be(expected.ReadConsistencyStrategy);
            (request?.SessionToken).Should().Be(expected.SessionToken);
        }
        (many.Single()?.ReadConsistencyStrategy).Should().Be(expected.ReadConsistencyStrategy);
        (many.Single()?.SessionToken).Should().Be(expected.SessionToken);
        if (mode == 2)
        {
            items.Should().OnlyContain(r => r == null);
            queries[0].Should().BeNull();
            queries[1].Should().BeNull();
            many.Single().Should().BeNull();
        }
        queries[2]!.MaxItemCount.Should().Be(25);
        queries[3]!.PartitionKey.Should().Be(new PartitionKey("pk"));
    }

    [Test]
    public async ValueTask QueryBuildersAndExplicitSdkOptionsRespectRepositoryPrecedence(CancellationToken cancellationToken)
    {
        var defaults = new CosmosReadOptions { ReadConsistencyStrategy = ReadConsistencyStrategy.LatestCommitted };
        var seen = new List<QueryRequestOptions?>();
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        container.Setup(c => c.GetItemLinqQueryable<TestDocument>(false, It.IsAny<string>(), It.IsAny<QueryRequestOptions>(), null))
            .Callback<bool, string, QueryRequestOptions, CosmosLinqSerializerOptions>((_, _, request, _) => seen.Add(request))
            .Returns(Array.Empty<TestDocument>().AsQueryable().OrderBy(d => d.Id));
        CaptureQueryOptions<IdPartitionPair>(container, seen);
        var repo = CreateRepository(container.Object, defaults);
        var explicitOptions = new QueryRequestOptions { MaxItemCount = 17, PartitionKey = new PartitionKey("pk") };

        await repo.BuildQueryable(cancellationToken: cancellationToken);
        await repo.BuildQueryable(explicitOptions, cancellationToken: cancellationToken);
        await repo.BuildPagedQueryable(25, cancellationToken: cancellationToken);
        await repo.BuildPagedQueryable(25, readOptions: new CosmosReadOptions(), cancellationToken: cancellationToken);
        await repo.GetIds(new QueryDefinition("SELECT * FROM c"), cancellationToken: cancellationToken);
        await repo.GetIds(new QueryDefinition("SELECT * FROM c"), explicitOptions, cancellationToken: cancellationToken);
        var overrideOptions = new CosmosReadOptions { ReadConsistencyStrategy = ReadConsistencyStrategy.Eventual, SessionToken = "0:42" };
        await repo.BuildQueryable(readOptions: overrideOptions, cancellationToken: cancellationToken);
        await repo.BuildQueryable<TestDocument>(readOptions: overrideOptions, cancellationToken: cancellationToken);
        await repo.BuildQueryable(readOptions: new CosmosReadOptions(), cancellationToken: cancellationToken);
        await repo.BuildQueryable(explicitOptions, readOptions: overrideOptions, cancellationToken: cancellationToken);

        seen.Count.Should().Be(10);
        AssertQueryReadOptions(seen[0]!, defaults);
        seen[1].Should().BeSameAs(explicitOptions);
        AssertQueryReadOptions(seen[2]!, defaults);
        seen[2]!.MaxItemCount.Should().Be(25);
        AssertQueryReadOptions(seen[3]!, null);
        seen[3]!.MaxItemCount.Should().Be(25);
        AssertQueryReadOptions(seen[4]!, defaults);
        seen[5].Should().BeSameAs(explicitOptions);
        AssertQueryReadOptions(seen[6]!, overrideOptions);
        AssertQueryReadOptions(seen[7]!, overrideOptions);
        seen[8].Should().BeNull();
        seen[9].Should().BeSameAs(explicitOptions);
        explicitOptions.ReadConsistencyStrategy.Should().BeNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    public async ValueTask RequireETagBlocksEveryUnconditionalWriteBeforeIo(int mode, CancellationToken cancellationToken)
    {
        var required = new CosmosWriteOptions { RequireETag = true };
        CosmosWriteOptions? methodOptions = mode switch
        {
            0 or 4 => required,
            2 => new CosmosWriteOptions(),
            3 => new CosmosWriteOptions { RequireETag = false },
            _ => null
        };
        var util = new Mock<ICosmosContainerUtil>(MockBehavior.Strict);
        var repo = new TestRepository(util.Object, writeOptions: mode == 0 ? null : required);
        var document = new TestDocument { DocumentId = "doc", PartitionKey = "pk" };
        List<TestDocument> documents = [document];
        List<IdPartitionPair> ids = [new() { Id = "doc", PartitionKey = "pk" }];
        IQueryable<TestDocument> query = documents.AsQueryable();
        var sql = new QueryDefinition("SELECT * FROM c");
        var container = new Mock<Microsoft.Azure.Cosmos.Container>(MockBehavior.Strict);
        Func<Task>[] operations =
        [
            () => repo.UpdateItem(document, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.UpdateItem("pk:doc", document, useQueue: true, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.UpdateItems(documents, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.UpdateItemsParallel(documents, 2, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.PatchItem("pk:doc", [], useQueue: true, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.PatchItems(documents, [], writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteItem("pk:doc", writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteItem("doc", "pk", useQueue: true, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteItemWithContainer(container.Object, "doc", "pk", writeOptions: methodOptions, ct: cancellationToken).AsTask(),
            () => repo.DeleteAll(writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteItems(query, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteItemsParallel(query, 2, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteIds(ids, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteIdsParallel(ids, 2, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteIdsBatched(ids, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteCreatedAtBetween(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteAllPaged(writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteItemsPaged(sql, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteAllPagedParallel(2, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask(),
            () => repo.DeleteItemsPagedParallel(sql, 2, writeOptions: methodOptions, cancellationToken: cancellationToken).AsTask()
        ];

        foreach (Func<Task> operation in operations)
            await operation.Should().ThrowAsync<InvalidOperationException>().WithMessage("*requires an ETag*");

        util.Invocations.Should().BeEmpty();
        container.Invocations.Should().BeEmpty();
    }

    [Test]
    public async ValueTask UnconditionalWritesRemainAllowedWhenNeitherLevelRequiresETags(CancellationToken cancellationToken)
    {
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        var document = new TestDocument { DocumentId = "doc", PartitionKey = "pk" };
        var response = new Mock<ItemResponse<TestDocument>>();
        response.SetupGet(r => r.Resource).Returns(document);
        container.Setup(c => c.ReplaceItemAsync(document, "doc", new PartitionKey("pk"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);
        container.Setup(c => c.PatchItemAsync<TestDocument>("doc", new PartitionKey("pk"), It.IsAny<IReadOnlyList<PatchOperation>>(),
            It.IsAny<PatchItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(response.Object);
        container.Setup(c => c.DeleteItemStreamAsync("doc", new PartitionKey("pk"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(new ResponseMessage(HttpStatusCode.NoContent)));
        var repo = CreateRepository(container.Object, writeOptions: new CosmosWriteOptions { RequireETag = false });
        var allow = new CosmosWriteOptions();

        await repo.UpdateItem(document, writeOptions: allow, cancellationToken: cancellationToken);
        await repo.UpdateItemsParallel([document], 2, writeOptions: allow, cancellationToken: cancellationToken);
        await repo.PatchItem("pk:doc", [], writeOptions: allow, cancellationToken: cancellationToken);
        await repo.PatchItems([document], [], writeOptions: allow, cancellationToken: cancellationToken);
        await repo.DeleteItem("pk:doc", writeOptions: allow, cancellationToken: cancellationToken);
        await repo.DeleteIdsParallel([new() { Id = "doc", PartitionKey = "pk" }], 2, writeOptions: allow, cancellationToken: cancellationToken);

        container.Verify(c => c.ReplaceItemAsync(document, "doc", new PartitionKey("pk"), null, It.IsAny<CancellationToken>()), Times.Exactly(2));
        container.Verify(c => c.PatchItemAsync<TestDocument>("doc", new PartitionKey("pk"), It.IsAny<IReadOnlyList<PatchOperation>>(), null,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        container.Verify(c => c.DeleteItemStreamAsync("doc", new PartitionKey("pk"), It.Is<ItemRequestOptions>(o => o.IfMatchEtag == null),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        Func<Task> strengthened = () => repo.UpdateItem(document, writeOptions: new CosmosWriteOptions { RequireETag = true }, cancellationToken: cancellationToken).AsTask();
        await strengthened.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async ValueTask RequiredETagsAllowConditionalWritesAndCreatesAndPropagateConflicts(CancellationToken cancellationToken)
    {
        var document = new TestDocument { DocumentId = "doc", PartitionKey = "pk" };
        var container = new Mock<Microsoft.Azure.Cosmos.Container>();
        var response = new Mock<ItemResponse<TestDocument>>();
        response.SetupGet(r => r.Resource).Returns(document);
        response.SetupGet(r => r.ETag).Returns("next");
        container.Setup(c => c.CreateItemAsync(document, It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);
        container.Setup(c => c.ReplaceItemAsync(document, "doc", new PartitionKey("pk"), It.Is<ItemRequestOptions>(o => o.IfMatchEtag == "etag"),
            It.IsAny<CancellationToken>())).ReturnsAsync(response.Object);
        container.Setup(c => c.PatchItemAsync<TestDocument>("doc", new PartitionKey("pk"), It.IsAny<IReadOnlyList<PatchOperation>>(),
            It.Is<PatchItemRequestOptions>(o => o.IfMatchEtag == "etag"), It.IsAny<CancellationToken>())).ReturnsAsync(response.Object);
        container.Setup(c => c.DeleteItemStreamAsync("doc", new PartitionKey("pk"), It.Is<ItemRequestOptions>(o => o.IfMatchEtag == "etag"),
            It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new ResponseMessage(HttpStatusCode.NoContent)));
        var repo = CreateRepository(container.Object, writeOptions: new CosmosWriteOptions { RequireETag = true });

        await repo.AddItem(document, cancellationToken: cancellationToken);
        await repo.UpdateItemIfMatch(document, "etag", cancellationToken: cancellationToken);
        await repo.PatchItemIfMatch("pk:doc", [], "etag", cancellationToken: cancellationToken);
        await repo.DeleteItemIfMatch("pk:doc", "etag", cancellationToken: cancellationToken);

        container.Setup(c => c.ReplaceItemAsync(document, "doc", new PartitionKey("pk"), It.Is<ItemRequestOptions>(o => o.IfMatchEtag == "stale"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Conflict", HttpStatusCode.PreconditionFailed, 0, "activity", 0));
        Func<Task> stale = () => repo.UpdateItemIfMatch(document, "stale", cancellationToken: cancellationToken).AsTask();
        await stale.Should().ThrowAsync<CosmosException>().Where(e => e.StatusCode == HttpStatusCode.PreconditionFailed);
    }
}

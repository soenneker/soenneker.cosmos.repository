using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Moq;
using Soenneker.Cosmos.Container.Abstract;
using Soenneker.Enums.CrudEventTypes;
using Soenneker.Json.OptionsCollection;
using Soenneker.Utils.BackgroundQueue.Abstract;

namespace Soenneker.Cosmos.Repository.Tests;

public partial class PerformanceRegressionTests
{
    [Test]
    [Arguments("add")]
    [Arguments("update")]
    [Arguments("batch")]
    public async Task QueuedWritesUseCosmosJsonOptionsAndSnapshotDocuments(string operation)
    {
        var queue = new Mock<IBackgroundQueue>();
        var util = new Mock<ICosmosContainerUtil>();
        util.Setup(u => u.Get("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Microsoft.Azure.Cosmos.Container>());
        var repository = new TestRepository(util.Object, backgroundQueue: queue.Object);
        var document = new TestDocument { PartitionKey = "pk", DocumentId = "doc", Updated = true };
        byte[] expected = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptionsCollection.WebOptions);

        switch (operation)
        {
            case "add": await repository.AddItem(document, useQueue: true); break;
            case "update": await repository.UpdateItem(document, useQueue: true); break;
            case "batch": await repository.UpdateItems([document], useQueue: true); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }

        document.Updated = false;
        GetQueuedJson(queue).Should().Equal(expected);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AuditSerializationSupportsApplicationObjectsAndJsonPayloads(bool useJson)
    {
        var queue = new Mock<IBackgroundQueue>();
        var util = new Mock<ICosmosContainerUtil>();
        util.Setup(u => u.Get("audits", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Microsoft.Azure.Cosmos.Container>());
        var repository = new TestRepository(util.Object, backgroundQueue: queue.Object);

        if (useJson)
            await repository.CreateAuditItem(CrudEventType.Create, "pk:doc", "{\"displayName\":\"Ada\"}");
        else
            await repository.CreateAuditItem(CrudEventType.Create, "pk:doc", new { DisplayName = "Ada" });

        using JsonDocument json = JsonDocument.Parse(GetQueuedJson(queue));
        json.RootElement.GetProperty("entity").GetProperty("displayName").GetString().Should().Be("Ada");
        json.RootElement.GetProperty("entityId").GetString().Should().Be("pk:doc");
    }

    private static byte[] GetQueuedJson(Mock<IBackgroundQueue> queue)
    {
        var state = (ITuple)queue.Invocations.Single().Arguments[0]!;
        return Enumerable.Range(0, state.Length).Select(i => state[i]).OfType<byte[]>().Single();
    }
}

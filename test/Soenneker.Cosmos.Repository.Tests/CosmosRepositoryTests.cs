using Soenneker.Tests.HostedUnit;

namespace Soenneker.Cosmos.Repository.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class CosmosRepositoryTests : HostedUnitTest
{
    public CosmosRepositoryTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {

    }
}

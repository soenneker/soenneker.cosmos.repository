using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.Cosmos.Repository.Tests;

[Collection("Collection")]
public class CosmosRepositoryTests : FixturedUnitTest
{
    public CosmosRepositoryTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public void Default()
    {

    }
}

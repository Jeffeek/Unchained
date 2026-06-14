using Shouldly;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Shapes;

public sealed class ConnectionEndpointTests
{
    [Fact]
    public void Constructor_StoresFields()
    {
        var endpoint = new ConnectionEndpoint(42, 3);
        endpoint.TargetShapeId.ShouldBe(42u);
        endpoint.ConnectionPointIndex.ShouldBe(3);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() =>
        new ConnectionEndpoint(1, 2).ShouldBe(new ConnectionEndpoint(1, 2));

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual() =>
        new ConnectionEndpoint(1, 2).ShouldNotBe(new ConnectionEndpoint(1, 3));
}

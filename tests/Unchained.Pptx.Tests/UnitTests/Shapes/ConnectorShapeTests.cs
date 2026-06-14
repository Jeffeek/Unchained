using Shouldly;
using Unchained.Pptx.Models.Shapes;
using Unchained.Pptx.Shapes;
using Xunit;

namespace Unchained.Pptx.Tests.UnitTests.Shapes;

public sealed class ConnectorShapeTests
{
    [Fact]
    public void Defaults_StraightNoConnections()
    {
        var connector = new ConnectorShape();
        connector.ConnectorType.ShouldBe(ConnectorType.Straight);
        connector.StartConnection.ShouldBeNull();
        connector.EndConnection.ShouldBeNull();
    }

    [Fact]
    public void Connections_RoundTrip()
    {
        var connector = new ConnectorShape
        {
            ConnectorType = ConnectorType.Bent,
            StartConnection = new ConnectionEndpoint(5, 0),
            EndConnection = new ConnectionEndpoint(9, 2)
        };
        connector.ConnectorType.ShouldBe(ConnectorType.Bent);
        connector.StartConnection.ShouldBe(new ConnectionEndpoint(5, 0));
        connector.EndConnection!.TargetShapeId.ShouldBe(9u);
        connector.EndConnection.ConnectionPointIndex.ShouldBe(2);
    }
}

using Unchained.Pptx.Models.Shapes;

namespace Unchained.Pptx.Shapes;

/// <summary>
///     A line connector that joins two points on a slide, optionally snapped to
///     connection points on source and target shapes.
/// </summary>
public sealed class ConnectorShape : Shape
{
    /// <summary>The routing style of the connector.</summary>
    public ConnectorType ConnectorType { get; set; } = ConnectorType.Straight;

    /// <summary>
    ///     The shape and connection point where the connector starts.
    ///     <see langword="null" /> when the start point is a free-floating coordinate.
    /// </summary>
    public ConnectionEndpoint? StartConnection { get; set; }

    /// <summary>
    ///     The shape and connection point where the connector ends.
    ///     <see langword="null" /> when the end point is a free-floating coordinate.
    /// </summary>
    public ConnectionEndpoint? EndConnection { get; set; }
}

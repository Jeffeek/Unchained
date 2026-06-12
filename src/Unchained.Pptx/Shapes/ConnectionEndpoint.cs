namespace Unchained.Pptx.Shapes;

/// <summary>
///     Identifies the target shape and the specific connection point index where a
///     <see cref="ConnectorShape" /> is anchored.
/// </summary>
/// <param name="TargetShapeId">The <see cref="Shape.ShapeId" /> of the shape being connected to.</param>
/// <param name="ConnectionPointIndex">
///     Zero-based index of the connection point on the target shape.
///     Typical shapes have four connection points (0 = top, 1 = right, 2 = bottom, 3 = left).
/// </param>
public sealed record ConnectionEndpoint(uint TargetShapeId, int ConnectionPointIndex);

namespace Unchained.Pptx.Models.Shapes;

/// <summary>Specifies the style of a connector shape between two points.</summary>
public enum ConnectorType
{
    /// <summary>A straight line connecting the two endpoints.</summary>
    Straight,

    /// <summary>
    ///     A connector with one or more right-angle bends (elbow connector).
    ///     The exact path is calculated automatically.
    /// </summary>
    Bent,

    /// <summary>A smoothly curved connector between the two endpoints.</summary>
    Curved
}

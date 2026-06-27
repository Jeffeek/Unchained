using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

/// <summary>Base type for parsed formula AST nodes.</summary>
internal abstract class FormulaNode;

internal sealed class NumberNode(double value) : FormulaNode
{
    public double Value { get; } = value;
}

internal sealed class TextNode(string value) : FormulaNode
{
    public string Value { get; } = value;
}

internal sealed class BooleanNode(bool value) : FormulaNode
{
    public bool Value { get; } = value;
}

internal sealed class ErrorNode(CellError error) : FormulaNode
{
    public CellError Error { get; } = error;
}

/// <summary>A single cell reference (<c>A1</c>, <c>$B$2</c>) or a defined name.</summary>
internal sealed class ReferenceNode(string text) : FormulaNode
{
    public string Text { get; } = text;
}

/// <summary>A range reference built from <c>left:right</c>.</summary>
internal sealed class RangeNode(FormulaNode start, FormulaNode end) : FormulaNode
{
    public FormulaNode Start { get; } = start;
    public FormulaNode End { get; } = end;
}

internal sealed class UnaryNode(string op, FormulaNode operand) : FormulaNode
{
    public string Operator { get; } = op;
    public FormulaNode Operand { get; } = operand;
}

internal sealed class BinaryNode(string op, FormulaNode left, FormulaNode right) : FormulaNode
{
    public string Operator { get; } = op;
    public FormulaNode Left { get; } = left;
    public FormulaNode Right { get; } = right;
}

internal sealed class FunctionNode(string name, IReadOnlyList<FormulaNode> arguments) : FormulaNode
{
    public string Name { get; } = name;
    public IReadOnlyList<FormulaNode> Arguments { get; } = arguments;
}

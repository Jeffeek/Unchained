using Unchained.Pdf.Core;

namespace Unchained.Pdf.Models;

/// <summary>
/// A single instruction in a PDF content stream (ISO 32000-1 §7.8.2):
/// zero or more <see cref="Operands"/> followed by a <see cref="Name"/> keyword.
/// <para>
/// Operands are raw <see cref="PdfObject"/> values — integers, reals, names, strings,
/// or arrays. The caller is responsible for casting them to the expected type based
/// on the operator's specification in Table A.1.
/// </para>
/// </summary>
/// <param name="Name">The operator keyword, e.g. <c>Tj</c>, <c>cm</c>, <c>BT</c>.</param>
/// <param name="Operands">The operand values that precede this operator in the stream.</param>
public sealed record ContentOperator(
    string Name,
    IReadOnlyList<PdfObject> Operands
)
{
    /// <summary><see langword="true"/> when this operator has at least one operand.</summary>
    public bool HasOperands => Operands.Count > 0;
}

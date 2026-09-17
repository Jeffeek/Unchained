using Shouldly;
using Unchained.Xlsx.Engine;
using Unchained.Xlsx.Tests.Helpers;
using Xunit;

namespace Unchained.Xlsx.Tests.IntegrationTests;

/// <summary>
///     Exercises <see cref="Unchained.Xlsx.Formulas.FormulaEvaluator" /> value-coercion helpers:
///     number/boolean → text via concatenation, and number/text → boolean via IF conditions.
/// </summary>
public sealed class FormulaEvaluatorCoercionTests
{
    private static object? Eval(string formula)
    {
        using var document = XlsxFixtures.WithSheets("Sheet1");
        return SpreadsheetDocument.EvaluateFormula(document.Sheets[0], formula);
    }

    [
        Theory,
        InlineData("=\"a\"&5", "a5"),         // number → text
        InlineData("=\"a\"&(1=1)", "aTRUE"),  // boolean → text
        InlineData("=\"a\"&(1=2)", "aFALSE")
    ]
    public void Concatenation_CoercesOperandsToText(string formula, string expected) => Eval(formula).ShouldBe(expected);

    [
        Theory,
        InlineData("=IF(5,\"y\",\"n\")", "y"),      // non-zero number → true
        InlineData("=IF(0,\"y\",\"n\")", "n"),      // zero → false
        InlineData("=IF(\"TRUE\",\"y\",\"n\")", "y"), // parseable text → true
        InlineData("=IF(\"nope\",\"y\",\"n\")", "n")  // unparseable text → false
    ]
    public void If_CoercesConditionToBoolean(string formula, string expected) => Eval(formula).ShouldBe(expected);
}

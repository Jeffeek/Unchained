using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

/// <summary>Thrown internally when a formula cannot be parsed; the evaluator maps it to <c>#NAME?</c>.</summary>
internal sealed class FormulaParseException(string message) : Exception(message);

/// <summary>
///     A precedence-climbing (Pratt) parser turning a token list into a <see cref="FormulaNode" />
///     tree. Operator precedence follows Excel: <c>:</c> &gt; unary <c>-</c> &gt; <c>%</c> &gt;
///     <c>^</c> &gt; <c>* /</c> &gt; <c>+ -</c> &gt; <c>&amp;</c> &gt; comparisons.
/// </summary>
internal sealed class FormulaParser
{
    private readonly List<FormulaToken> _tokens;
    private int _pos;

    private FormulaParser(List<FormulaToken> tokens) => _tokens = tokens;

    private FormulaToken Current => _tokens[_pos];

    public static FormulaNode Parse(string formula)
    {
        var tokens = FormulaTokenizer.Tokenize(formula);
        var parser = new FormulaParser(tokens);
        var node = parser.ParseExpression(0);
        return parser.Current.Type != FormulaTokenType.End
            ? throw new FormulaParseException($"Unexpected token '{parser.Current.Text}'.")
            : node;
    }

    private FormulaToken Advance() => _tokens[_pos++];

    // Binary operator precedence (higher binds tighter).
    private static int Precedence(string op) => op switch
    {
        "=" or "<>" or "<" or ">" or "<=" or ">=" => 1,
        "&" => 2,
        "+" or "-" => 3,
        "*" or "/" => 4,
        "^" => 5,
        _ => 0
    };

    private static bool IsRightAssociative(string op) => op == "^";

    private FormulaNode ParseExpression(int minPrecedence)
    {
        var left = ParseUnary();

        while (Current.Type == FormulaTokenType.Operator)
        {
            var op = Current.Text;
            var prec = Precedence(op);
            if (prec == 0 || prec < minPrecedence)
                break;

            Advance();
            var nextMin = IsRightAssociative(op) ? prec : prec + 1;
            var right = ParseExpression(nextMin);
            left = new BinaryNode(op, left, right);
        }

        return left;
    }

    private FormulaNode ParseUnary()
    {
        if (Current is { Type: FormulaTokenType.Operator, Text: "-" or "+" })
        {
            var op = Advance().Text;
            return new UnaryNode(op, ParseUnary());
        }

        var primary = ParsePrimary();

        // Postfix percent.
        while (Current is { Type: FormulaTokenType.Operator, Text: "%" })
        {
            Advance();
            primary = new UnaryNode("%", primary);
        }

        // Range operator binds references: A1:B2.
        while (Current.Type == FormulaTokenType.Colon)
        {
            Advance();
            var end = ParsePrimary();
            primary = new RangeNode(primary, end);
        }

        return primary;
    }

    private FormulaNode ParsePrimary()
    {
        var token = Current;
        switch (token.Type)
        {
            case FormulaTokenType.Number:
                Advance();
                return new NumberNode(FormulaTokenizer.ParseNumber(token.Text));
            case FormulaTokenType.Text:
                Advance();
                return new TextNode(token.Text);
            case FormulaTokenType.Boolean:
                Advance();
                return new BooleanNode(token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase));
            case FormulaTokenType.Error:
                Advance();
                return new ErrorNode(CellErrorExtensions.FromLiteral(token.Text) ?? CellError.Value);
            case FormulaTokenType.CellOrName:
                Advance();
                return new ReferenceNode(token.Text);
            case FormulaTokenType.Function:
                return ParseFunction();
            case FormulaTokenType.OpenParen:
            {
                Advance();
                var inner = ParseExpression(0);
                Expect(FormulaTokenType.CloseParen);
                return inner;
            }
            case FormulaTokenType.Operator:
            case FormulaTokenType.Colon:
            case FormulaTokenType.Comma:
            case FormulaTokenType.CloseParen:
            case FormulaTokenType.End:
            default:
                throw new FormulaParseException($"Unexpected token '{token.Text}'.");
        }
    }

    private FormulaNode ParseFunction()
    {
        var name = Advance().Text; // function name
        Expect(FormulaTokenType.OpenParen);

        var args = new List<FormulaNode>();
        if (Current.Type != FormulaTokenType.CloseParen)
        {
            args.Add(ParseExpression(0));
            while (Current.Type == FormulaTokenType.Comma)
            {
                Advance();
                args.Add(ParseExpression(0));
            }
        }

        Expect(FormulaTokenType.CloseParen);
        return new FunctionNode(name, args);
    }

    private void Expect(FormulaTokenType type)
    {
        if (Current.Type != type)
            throw new FormulaParseException($"Expected {type} but found '{Current.Text}'.");

        Advance();
    }
}

using System.Globalization;
using System.Text;

namespace Unchained.Xlsx.Formulas;

internal enum FormulaTokenType
{
    Number, Text, Boolean, Error,
    CellOrName,      // A1, $B$2, or a defined name / function name
    Function,        // identifier immediately followed by '('
    Operator,        // + - * / ^ & = <> < > <= >= %
    Colon,           // range operator
    Comma,           // argument separator
    OpenParen, CloseParen,
    End
}

internal readonly struct FormulaToken(FormulaTokenType type, string text)
{
    public FormulaTokenType Type { get; } = type;
    public string Text { get; } = text;
    public override string ToString() => $"{Type}:{Text}";
}

/// <summary>
///     Tokenizes an Excel formula string (without the leading <c>=</c>). Handles numbers, quoted
///     text (<c>"" </c> escapes), booleans, error literals, cell references / names, function names,
///     operators, and parentheses.
/// </summary>
internal static class FormulaTokenizer
{
    public static List<FormulaToken> Tokenize(string formula)
    {
        var tokens = new List<FormulaToken>();
        var i = 0;
        var n = formula.Length;

        while (i < n)
        {
            var c = formula[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    tokens.Add(ReadString(formula, ref i));
                    continue;
                case '#':
                    tokens.Add(ReadError(formula, ref i));
                    continue;
                case '(':
                    tokens.Add(new FormulaToken(FormulaTokenType.OpenParen, "("));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new FormulaToken(FormulaTokenType.CloseParen, ")"));
                    i++;
                    continue;
                case ',':
                    tokens.Add(new FormulaToken(FormulaTokenType.Comma, ","));
                    i++;
                    continue;
                case ':':
                    tokens.Add(new FormulaToken(FormulaTokenType.Colon, ":"));
                    i++;
                    continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(formula[i + 1])))
            {
                tokens.Add(ReadNumber(formula, ref i));
                continue;
            }

            if (TryReadOperator(formula, ref i, out var op))
            {
                tokens.Add(op);
                continue;
            }

            if (c == '\'' || c == '_' || char.IsLetter(c) || c == '$')
            {
                tokens.Add(ReadIdentifier(formula, ref i));
                continue;
            }

            // Unknown character — skip to avoid an infinite loop.
            i++;
        }

        tokens.Add(new FormulaToken(FormulaTokenType.End, string.Empty));
        return tokens;
    }

    private static FormulaToken ReadString(string s, ref int i)
    {
        var sb = new StringBuilder();
        i++; // opening quote
        while (i < s.Length)
        {
            if (s[i] == '"')
            {
                if (i + 1 < s.Length && s[i + 1] == '"')
                {
                    sb.Append('"');
                    i += 2;
                    continue;
                }

                i++; // closing quote
                break;
            }

            sb.Append(s[i]);
            i++;
        }

        return new FormulaToken(FormulaTokenType.Text, sb.ToString());
    }

    private static FormulaToken ReadError(string s, ref int i)
    {
        // Error literals: #DIV/0!, #N/A, #NAME?, #NULL!, #NUM!, #REF!, #VALUE!
        var start = i;
        i++; // '#'
        while (i < s.Length && s[i] is not (',' or ')' or ' ' or '('))
        {
            var ch = s[i];
            i++;
            if (ch is '!' or '?') break; // terminators that are part of the literal
        }

        return new FormulaToken(FormulaTokenType.Error, s[start..i]);
    }

    private static FormulaToken ReadNumber(string s, ref int i)
    {
        var start = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;

        // Scientific notation.
        if (i >= s.Length || (s[i] != 'e' && s[i] != 'E'))
            return new FormulaToken(FormulaTokenType.Number, s[start..i]);

        i++;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
        while (i < s.Length && char.IsDigit(s[i])) i++;

        return new FormulaToken(FormulaTokenType.Number, s[start..i]);
    }

    private static bool TryReadOperator(string s, ref int i, out FormulaToken token)
    {
        var c = s[i];
        // Two-char operators first.
        if (i + 1 < s.Length)
        {
            var two = s.Substring(i, 2);
            if (two is "<>" or "<=" or ">=")
            {
                i += 2;
                token = new FormulaToken(FormulaTokenType.Operator, two);
                return true;
            }
        }

        if (c is '+' or '-' or '*' or '/' or '^' or '&' or '=' or '<' or '>' or '%')
        {
            i++;
            token = new FormulaToken(FormulaTokenType.Operator, c.ToString());
            return true;
        }

        token = default;
        return false;
    }

    private static FormulaToken ReadIdentifier(string s, ref int i)
    {
        var start = i;

        // Sheet-qualified name with quotes: 'My Sheet'!A1
        if (s[i] == '\'')
        {
            i++;
            while (i < s.Length && s[i] != '\'') i++;
            if (i < s.Length) i++; // closing quote
            if (i < s.Length && s[i] == '!') i++;
        }

        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsLetterOrDigit(c) || c is '$' or '.' or '_' or '!' or '\\')
            {
                i++;
                continue;
            }

            break;
        }

        var text = s[start..i];

        // Boolean literals.
        return text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || text.Equals("FALSE", StringComparison.OrdinalIgnoreCase)
            ? new FormulaToken(FormulaTokenType.Boolean, text)
            :
            // Function call when the identifier is immediately followed by '('.
            i < s.Length && s[i] == '('
                ? new FormulaToken(FormulaTokenType.Function, text)
                : new FormulaToken(FormulaTokenType.CellOrName, text);
    }

    public static double ParseNumber(string text) =>
        double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}

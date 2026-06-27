using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

internal static partial class FormulaFunctions
{
    // ── Logical ─────────────────────────────────────────────────────────────────

    private static FormulaValue If(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var condition = ev.Evaluate(args[0]);
        return condition.IsError
            ? condition
            : FormulaEvaluator.ToBoolean(condition)
                ? ev.Evaluate(args[1])
                : args.Count >= 3
                    ? ev.Evaluate(args[2])
                    : FormulaValue.FromBoolean(false);
    }

    private static FormulaValue Ifs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        for (var i = 0; i + 1 < args.Count; i += 2)
        {
            var condition = ev.Evaluate(args[i]);
            if (condition.IsError) return condition;
            if (FormulaEvaluator.ToBoolean(condition))
                return ev.Evaluate(args[i + 1]);
        }

        return FormulaValue.FromError(CellError.NotAvailable);
    }

    private static FormulaValue Switch(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var subject = ev.Evaluate(args[0]);
        var i = 1;
        for (; i + 1 < args.Count; i += 2)
        {
            var candidate = ev.Evaluate(args[i]);
            if (ScalarEquals(subject, candidate))
                return ev.Evaluate(args[i + 1]);
        }

        // A trailing odd argument is the default.
        return i < args.Count ? ev.Evaluate(args[i]) : FormulaValue.FromError(CellError.NotAvailable);
    }

    private static FormulaValue IfError(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev, bool naOnly)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var value = ev.Evaluate(args[0]);
        var caught = value.IsError && (!naOnly || value.Error == CellError.NotAvailable);
        return caught ? ev.Evaluate(args[1]) : value;
    }

    private enum BoolMode { And, Or, Xor }

    private static FormulaValue BoolAggregate(IEnumerable<FormulaNode> args, FormulaEvaluator ev, BoolMode mode)
    {
        var any = false;
        var trueCount = 0;
        foreach (var v in args.SelectMany(arg => ev.Evaluate(arg).Flatten()))
        {
            if (v.IsError) return v;

            if (v.Kind == FormulaValueKind.Blank) continue;

            any = true;
            var truthy = FormulaEvaluator.ToBoolean(v);
            if (truthy) trueCount++;
            switch (mode)
            {
                case BoolMode.And when !truthy:
                    return FormulaValue.FromBoolean(false);
                case BoolMode.Or when truthy:
                    return FormulaValue.FromBoolean(true);
                case BoolMode.And:
                case BoolMode.Or:
                case BoolMode.Xor:
                break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        return mode switch
        {
            BoolMode.And => FormulaValue.FromBoolean(any),
            BoolMode.Or => FormulaValue.FromBoolean(false),
            _ => FormulaValue.FromBoolean(trueCount % 2 == 1)
        };
    }
}
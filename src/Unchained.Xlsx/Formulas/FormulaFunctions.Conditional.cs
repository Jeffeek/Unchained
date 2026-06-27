using Unchained.Xlsx.Models.Cell;

namespace Unchained.Xlsx.Formulas;

internal static partial class FormulaFunctions
{
    // ── Conditional aggregates ──────────────────────────────────────────────────

    private static FormulaValue SumIf(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var range = ev.Evaluate(args[0]).Flatten().ToList();
        var criterion = ev.Evaluate(args[1]);
        var sumRange = args.Count >= 3 ? ev.Evaluate(args[2]).Flatten().ToList() : range;

        var total = 0.0;
        for (var i = 0; i < range.Count; i++)
        {
            if (MatchesCriterion(range[i], criterion) && i < sumRange.Count && IsNumber(sumRange[i]))
                total += FormulaEvaluator.ToNumber(sumRange[i]);
        }

        return Number(total);
    }

    private static FormulaValue AverageIf(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var range = ev.Evaluate(args[0]).Flatten().ToList();
        var criterion = ev.Evaluate(args[1]);
        var avgRange = args.Count >= 3 ? ev.Evaluate(args[2]).Flatten().ToList() : range;

        double total = 0;
        var count = 0;
        for (var i = 0; i < range.Count; i++)
        {
            if (!MatchesCriterion(range[i], criterion) || i >= avgRange.Count || !IsNumber(avgRange[i])) continue;

            total += FormulaEvaluator.ToNumber(avgRange[i]);
            count++;
        }

        return count == 0 ? FormulaValue.FromError(CellError.DivisionByZero) : Number(total / count);
    }

    private static FormulaValue CountIf(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var range = ev.Evaluate(args[0]).Flatten().ToList();
        var criterion = ev.Evaluate(args[1]);
        return Number(range.Count(v => MatchesCriterion(v, criterion)));
    }

    private static FormulaValue ConditionalSumIfs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev, bool average)
    {
        // SUMIFS(sumRange, critRange1, crit1, ...); AVERAGEIFS shares the layout.
        if (args.Count < 3) return FormulaValue.FromError(CellError.Value);

        var aggregate = ev.Evaluate(args[0]).Flatten().ToList();
        var pairs = CriteriaPairs(args, ev, 1);

        double total = 0;
        var count = 0;
        for (var i = 0; i < aggregate.Count; i++)
        {
            if (!RowMatchesAll(pairs, i)) continue;
            if (!IsNumber(aggregate[i])) continue;

            total += FormulaEvaluator.ToNumber(aggregate[i]);
            count++;
        }

        return !average
            ? Number(total)
            : count == 0
                ? FormulaValue.FromError(CellError.DivisionByZero)
                : Number(total / count);
    }

    private static FormulaValue CountIfs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var pairs = CriteriaPairs(args, ev, 0);
        if (pairs.Count == 0) return Number(0);

        var length = pairs.Min(static p => p.Range.Count);
        var count = 0;
        for (var i = 0; i < length; i++)
        {
            if (RowMatchesAll(pairs, i))
                count++;
        }

        return Number(count);
    }

    private static List<(List<FormulaValue> Range, FormulaValue Criterion)> CriteriaPairs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev, int startIndex)
    {
        var pairs = new List<(List<FormulaValue>, FormulaValue)>();
        for (var i = startIndex; i + 1 < args.Count; i += 2)
            pairs.Add((ev.Evaluate(args[i]).Flatten().ToList(), ev.Evaluate(args[i + 1])));
        return pairs;
    }

    private static bool RowMatchesAll(IEnumerable<(List<FormulaValue> Range, FormulaValue Criterion)> pairs, int row) =>
        pairs.All(p => row < p.Range.Count && MatchesCriterion(p.Range[row], p.Criterion));

    private static FormulaValue SumProduct(IReadOnlyCollection<FormulaNode> args, FormulaEvaluator ev)
    {
        if (args.Count == 0) return Number(0);

        var arrays = args.Select(a => ev.Evaluate(a).Flatten().ToList()).ToList();
        var length = arrays.Min(static a => a.Count);
        double total = 0;
        for (var i = 0; i < length; i++)
        {
            var product = arrays.Aggregate(1.0, (current, arr) => current * FormulaEvaluator.ToNumber(arr[i]));
            total += product;
        }

        return Number(total);
    }

    private static FormulaValue MinMaxIfs(IReadOnlyList<FormulaNode> args, FormulaEvaluator ev, bool max)
    {
        if (args.Count < 2) return FormulaValue.FromError(CellError.Value);

        var values = ev.Evaluate(args[0]).Flatten().ToList();
        var pairs = CriteriaPairs(args, ev, 1);
        var length = pairs.Min(static p => p.Range.Count);
        var min = double.MaxValue;
        var maxVal = double.MinValue;
        var count = 0;

        for (var i = 0; i < length; i++)
        {
            if (!RowMatchesAll(pairs, i)) continue;
            if (!IsNumber(values[i])) continue;

            var v = FormulaEvaluator.ToNumber(values[i]);
            if (max)
            {
                if (v > maxVal) maxVal = v;
            }
            else
            {
                if (v < min) min = v;
            }

            count++;
        }

        return count == 0
            ? FormulaValue.FromError(CellError.DivisionByZero)
            : Number(max ? maxVal : min);
    }
}

using System.Text.Json;

namespace Nexus.Fms.Core.Engine;

/// <summary>
/// Pure evaluator for a <see cref="RulePredicate"/> against a flat dictionary of facts.
/// Keeping it pure (no I/O) keeps synchronous screening within the 50ms budget (NFR-01)
/// and makes rules unit-testable.
/// </summary>
public static class PredicateEvaluator
{
    public static bool Evaluate(RulePredicate predicate, IReadOnlyDictionary<string, object?> facts)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (predicate.All is { Count: > 0 } all)
            return all.All(p => Evaluate(p, facts));

        if (predicate.Any is { Count: > 0 } any)
            return any.Any(p => Evaluate(p, facts));

        if (predicate.Not is { } not)
            return !Evaluate(not, facts);

        // Leaf comparison
        if (string.IsNullOrWhiteSpace(predicate.Fact) || predicate.Op is null)
            return false;

        facts.TryGetValue(predicate.Fact, out var actual);
        return Compare(actual, predicate.Op.Value, predicate.Value);
    }

    private static bool Compare(object? actual, PredicateOp op, JsonElement? expected)
    {
        switch (op)
        {
            case PredicateOp.IsTrue:
                return actual is bool bt && bt;
            case PredicateOp.IsFalse:
                return actual is bool bf && !bf;
        }

        if (expected is null)
            return false;

        var exp = expected.Value;

        if (op == PredicateOp.In)
        {
            if (exp.ValueKind != JsonValueKind.Array) return false;
            foreach (var item in exp.EnumerateArray())
                if (ScalarEquals(actual, item)) return true;
            return false;
        }

        if (op is PredicateOp.Eq or PredicateOp.Ne)
        {
            var equal = ScalarEquals(actual, exp);
            return op == PredicateOp.Eq ? equal : !equal;
        }

        // Numeric comparisons
        if (!TryToDouble(actual, out var a) || !TryJsonToDouble(exp, out var b))
            return false;

        return op switch
        {
            PredicateOp.Gt => a > b,
            PredicateOp.Gte => a >= b,
            PredicateOp.Lt => a < b,
            PredicateOp.Lte => a <= b,
            _ => false
        };
    }

    private static bool ScalarEquals(object? actual, JsonElement expected)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.True:
                return actual is bool b && b;
            case JsonValueKind.False:
                return actual is bool b2 && !b2;
            case JsonValueKind.Number:
                return TryToDouble(actual, out var a) && expected.TryGetDouble(out var e) && Math.Abs(a - e) < 1e-9;
            case JsonValueKind.String:
                return string.Equals(actual?.ToString(), expected.GetString(), StringComparison.OrdinalIgnoreCase);
            case JsonValueKind.Null:
                return actual is null;
            default:
                return false;
        }
    }

    private static bool TryToDouble(object? value, out double result)
    {
        switch (value)
        {
            case null:
                result = 0;
                return false;
            case bool b:
                result = b ? 1 : 0;
                return true;
            default:
                return double.TryParse(
                    Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out result);
        }
    }

    private static bool TryJsonToDouble(JsonElement el, out double result)
    {
        if (el.ValueKind == JsonValueKind.Number)
            return el.TryGetDouble(out result);
        if (el.ValueKind == JsonValueKind.String)
            return double.TryParse(el.GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result);
        result = 0;
        return false;
    }
}

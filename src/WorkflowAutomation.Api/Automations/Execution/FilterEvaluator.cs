namespace WorkflowAutomation.Api.Automations.Execution;

public static class FilterEvaluator
{
    public static bool Passes(FilterConditionDto? filter, IReadOnlyDictionary<string, string> tokens)
    {
        if (filter is null) return true;
        if (!tokens.TryGetValue(filter.Field, out var actual)) return false;

        return filter.Operator switch
        {
            "contains" => actual.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
            "equals" => string.Equals(actual, filter.Value, StringComparison.OrdinalIgnoreCase),
            "gte" => Compare(actual, filter.Value) >= 0,
            "lte" => Compare(actual, filter.Value) <= 0,
            _ => false
        };
    }

    private static int Compare(string a, string b) =>
        decimal.TryParse(a, out var da) && decimal.TryParse(b, out var db)
            ? da.CompareTo(db)
            : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
}
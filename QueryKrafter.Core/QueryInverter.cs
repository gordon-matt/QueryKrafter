namespace QueryKrafter;

/// <summary>
/// Makes a group match exactly what it didn't. A NOT group just loses its NOT; otherwise AND and
/// OR swap and every rule takes its operator's <see cref="QueryOperator.Opposite"/>, all the way down.
/// </summary>
public static class QueryInverter
{
    /// <returns>An inverted copy. The group passed in is left as it is.</returns>
    public static Result<QueryGroup> Invert(QueryGroup group, QueryBuilderConfig config)
    {
        var errors = new List<string>();
        var inverted = InvertGroup(group, config, errors);
        return errors.Count > 0 ? Result<QueryGroup>.Error(new ErrorList(errors)) : inverted;
    }

    private static QueryGroup InvertGroup(QueryGroup group, QueryBuilderConfig config, List<string> errors)
    {
        var copy = group.Clone();
        if (copy.Not)
        {
            copy.Not = false;
            return copy;
        }

        if (string.Equals(copy.Condition, Constants.Conditions.And, StringComparison.OrdinalIgnoreCase))
        {
            copy.Condition = Constants.Conditions.Or;
        }
        else if (string.Equals(copy.Condition, Constants.Conditions.Or, StringComparison.OrdinalIgnoreCase))
        {
            copy.Condition = Constants.Conditions.And;
        }
        else
        {
            errors.Add($"The condition \"{copy.Condition}\" has no opposite.");
        }

        for (int i = 0; i < copy.Rules.Count; i++)
        {
            copy.Rules[i] = copy.Rules[i] switch
            {
                QueryGroup child => InvertGroup(child, config, errors),
                QueryRule rule => InvertRule(rule, config, errors),
                var other => other,
            };
        }

        return copy;
    }

    /// <summary>A rule whose operator has no opposite goes in a NOT group when groups can be negated.</summary>
    private static QueryNode InvertRule(QueryRule rule, QueryBuilderConfig config, List<string> errors)
    {
        if (string.IsNullOrEmpty(rule.Operator))
        {
            return rule;
        }

        if (config.FindOperator(rule.Operator)?.Opposite is { } opposite && config.FindOperator(opposite) is not null)
        {
            rule.Operator = opposite;
            return rule;
        }

        if (config.AllowNot)
        {
            return new QueryGroup { Not = true, Rules = [rule] };
        }

        errors.Add($"The operator \"{rule.Operator}\" has no opposite.");
        return rule;
    }
}

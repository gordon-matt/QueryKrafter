namespace QueryKrafter.Models;

/// <summary>
/// What the builder offers. The same configuration validates rules and turns them into SQL or
/// LINQ on the server, so keep it there rather than trusting one that came from the browser.
/// </summary>
public sealed record QueryBuilderConfig
{
    /// <summary>Null when JSON omits the property: the serializer uses the record constructor and skips the initializer.</summary>
    private readonly IReadOnlyList<QueryFilter>? filters;

    /// <summary>
    /// Gets or sets the filters.
    /// </summary>
    public IReadOnlyList<QueryFilter> Filters
    {
        get => filters ?? [];
        init => filters = value;
    }

    /// <summary>Defaults to <see cref="QueryOperators.Defaults"/>.</summary>
    public IReadOnlyList<QueryOperator>? Operators { get; init; }

    private readonly IReadOnlyList<string>? conditions;

    /// <summary>
    /// Gets or sets the conditions.
    /// </summary>
    public IReadOnlyList<string> Conditions
    {
        get => conditions is { Count: > 0 } ? conditions : [Constants.Conditions.And, Constants.Conditions.Or];
        init => conditions = value;
    }

    private readonly string? defaultCondition;

    /// <summary>
    /// Gets or sets the default condition.
    /// </summary>
    public string DefaultCondition
    {
        get => string.IsNullOrEmpty(defaultCondition) ? Constants.Conditions.And : defaultCondition;
        init => defaultCondition = value;
    }

    /// <summary>The filter a new rule starts on. Without it new rules start with no filter chosen.</summary>
    public string? DefaultFilter { get; init; }

    /// <summary>
    /// How deep groups can nest below the root: 0 for no groups, 1 for groups inside the root only,
    /// and <see cref="Constants.UnlimitedDepth"/> for no limit.
    /// </summary>
    public int MaxGroupDepth { get; init; } = Constants.UnlimitedDepth;

    /// <summary>Lets the root group have no rules, meaning "no filter".</summary>
    public bool AllowEmpty { get; init; }

    /// <summary>Shows a NOT toggle on each group.</summary>
    public bool AllowNot { get; init; }

    /// <summary>Shows a button on each group that inverts its logic.</summary>
    public bool AllowInvert { get; init; }

    /// <summary>Lets rules and groups be dragged to a new place.</summary>
    public bool AllowSorting { get; init; }

    /// <summary>Lists filters alphabetically within their groups.</summary>
    public bool SortFilters { get; init; }

    /// <summary>Offers an empty choice at the top of the field list.</summary>
    public bool DisplayEmptyFilter { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is display errors.
    /// </summary>
    public bool DisplayErrors { get; init; } = true;

    /// <summary>
    /// Gets or sets the default rule flags.
    /// </summary>
    public RuleFlags DefaultRuleFlags { get; init; } = RuleFlags.None;

    /// <summary>
    /// Gets or sets the default group flags.
    /// </summary>
    public GroupFlags DefaultGroupFlags { get; init; } = GroupFlags.None;

    /// <summary>Headings for <see cref="QueryFilter.Group"/> and <see cref="QueryOperator.Group"/> keys. Keys without one are shown as they are.</summary>
    public IReadOnlyDictionary<string, string>? GroupLabels { get; init; }

    /// <summary>
    /// Runs after the built-in checks pass. Return an error code and its arguments, or <c>null</c>.
    /// The code is looked up as <c>errors.{code}</c> in the language catalog.
    /// </summary>
    [JsonIgnore]
    public Func<QueryRule, QueryFilter, QueryRuleError?>? RuleValidator { get; init; }

    /// <summary>
    /// Gets the all operators.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<QueryOperator> AllOperators => Operators ?? QueryOperators.Defaults;

    /// <summary>
    /// Finds the filter.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The filter.</returns>
    public QueryFilter? FindFilter(string? id) =>
        string.IsNullOrEmpty(id) ? null : Filters.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Finds the operator.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The operator.</returns>
    public QueryOperator? FindOperator(string? type) =>
        string.IsNullOrEmpty(type) ? null : AllOperators.FirstOrDefault(o => string.Equals(o.Type, type, StringComparison.Ordinal));

    /// <summary>The operators a filter offers, in the filter's order when it names them.</summary>
    public IReadOnlyList<QueryOperator> OperatorsFor(QueryFilter filter)
    {
        if (filter.Operators is { } named)
        {
            return [.. named.Select(FindOperator).OfType<QueryOperator>()];
        }

        var kind = filter.Type.Kind;
        return [.. AllOperators.Where(o => o.AppliesTo.HasFlag(kind))];
    }

    /// <summary>
    /// Gets the default operator for.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>The default operator for.</returns>
    public QueryOperator? DefaultOperatorFor(QueryFilter filter)
    {
        var operators = OperatorsFor(filter);
        return operators.FirstOrDefault(o => string.Equals(o.Type, filter.DefaultOperator, StringComparison.Ordinal))
            ?? operators.FirstOrDefault();
    }

    /// <summary>
    /// Gets the group label.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The group label.</returns>
    public string GroupLabel(string key) => GroupLabels?.GetValueOrDefault(key) ?? key;

    /// <summary>Whether a group at <paramref name="level"/> (the root is 1) may contain another group.</summary>
    public bool CanAddGroupAt(int level) => MaxGroupDepth == Constants.UnlimitedDepth || MaxGroupDepth >= level;
}

/// <summary>A custom validation failure: an <c>errors.{Code}</c> key and the values for its placeholders.</summary>
public sealed record QueryRuleError(string Code, params IReadOnlyList<string> Arguments);

namespace QueryKrafter.Models;

/// <summary>Locks parts of a rule. In the rules JSON these are <c>flags</c>, or <c>readonly: true</c> for all of them.</summary>
public sealed record RuleFlags
{
    /// <summary>
    /// Gets the none.
    /// </summary>
    public static RuleFlags None { get; } = new();

    /// <summary>
    /// Gets the read only.
    /// </summary>
    public static RuleFlags ReadOnly { get; } = new() { FilterReadOnly = true, OperatorReadOnly = true, ValueReadOnly = true, NoDelete = true };

    /// <summary>
    /// Gets or sets a value indicating whether this instance is filter read only.
    /// </summary>
    public bool FilterReadOnly { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is operator read only.
    /// </summary>
    public bool OperatorReadOnly { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is value read only.
    /// </summary>
    public bool ValueReadOnly { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is no delete.
    /// </summary>
    public bool NoDelete { get; init; }

    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => !FilterReadOnly && !OperatorReadOnly && !ValueReadOnly && !NoDelete;
}

/// <summary>Locks parts of a group. In the rules JSON these are <c>flags</c>, or <c>readonly: true</c> for all of them.</summary>
public sealed record GroupFlags
{
    /// <summary>
    /// Gets the none.
    /// </summary>
    public static GroupFlags None { get; } = new();

    /// <summary>
    /// Gets the read only.
    /// </summary>
    public static GroupFlags ReadOnly { get; } = new() { ConditionReadOnly = true, NoAddRule = true, NoAddGroup = true, NoDelete = true };

    /// <summary>
    /// Gets or sets a value indicating whether this instance is condition read only.
    /// </summary>
    public bool ConditionReadOnly { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is no add rule.
    /// </summary>
    public bool NoAddRule { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is no add group.
    /// </summary>
    public bool NoAddGroup { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is no delete.
    /// </summary>
    public bool NoDelete { get; init; }

    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => !ConditionReadOnly && !NoAddRule && !NoAddGroup && !NoDelete;
}

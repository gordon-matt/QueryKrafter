namespace QueryKrafter.Models;

/// <summary>Locks parts of a rule. In the rules JSON these are <c>flags</c>, or <c>readonly: true</c> for all of them.</summary>
public sealed record RuleFlags
{
    public static RuleFlags None { get; } = new();

    public static RuleFlags ReadOnly { get; } = new() { FilterReadOnly = true, OperatorReadOnly = true, ValueReadOnly = true, NoDelete = true };

    public bool FilterReadOnly { get; init; }

    public bool OperatorReadOnly { get; init; }

    public bool ValueReadOnly { get; init; }

    public bool NoDelete { get; init; }

    [JsonIgnore]
    public bool IsEmpty => !FilterReadOnly && !OperatorReadOnly && !ValueReadOnly && !NoDelete;
}

/// <summary>Locks parts of a group. In the rules JSON these are <c>flags</c>, or <c>readonly: true</c> for all of them.</summary>
public sealed record GroupFlags
{
    public static GroupFlags None { get; } = new();

    public static GroupFlags ReadOnly { get; } = new() { ConditionReadOnly = true, NoAddRule = true, NoAddGroup = true, NoDelete = true };

    public bool ConditionReadOnly { get; init; }

    public bool NoAddRule { get; init; }

    public bool NoAddGroup { get; init; }

    public bool NoDelete { get; init; }

    [JsonIgnore]
    public bool IsEmpty => !ConditionReadOnly && !NoAddRule && !NoAddGroup && !NoDelete;
}

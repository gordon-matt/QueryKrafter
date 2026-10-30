using QueryKrafter.Serialization;

namespace QueryKrafter.Models;

/// <summary>A field the user can build rules on.</summary>
public sealed record QueryFilter
{
    /// <summary>Stored in each rule's <c>id</c>. Rules are matched to filters by this, never by their <c>field</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Shown in the field list. Defaults to <see cref="Id"/>.</summary>
    public string? Label { get; init; }

    /// <summary>
    /// The SQL the generator writes for this field, used verbatim, so it can be a quoted column,
    /// a qualified name or an expression. Comes from your configuration only: the generator
    /// ignores the <c>field</c> a rule arrives with. Defaults to <see cref="Id"/> as a quoted identifier.
    /// </summary>
    public string? Field { get; init; }

    /// <summary>The member path the LINQ builder filters on, e.g. <c>Customer.Name</c>. Defaults to <see cref="Id"/>.</summary>
    public string? Property { get; init; }

    public QueryValueType Type { get; init; } = QueryValueType.String;

    /// <summary>Defaults to the usual input for <see cref="Type"/>, or a drop-down when <see cref="Values"/> are given.</summary>
    public QueryInputType? Input { get; init; }

    /// <summary>The operator types offered, in this order. Defaults to every operator that applies to <see cref="Type"/>.</summary>
    public IReadOnlyList<string>? Operators { get; init; }

    public string? DefaultOperator { get; init; }

    /// <summary>The value a new rule on this filter starts with.</summary>
    public JsonNode? DefaultValue { get; init; }

    /// <summary>The choices for radio, checkbox and select inputs.</summary>
    /// <summary>Null when JSON omits it: deserialization goes through the record constructor, which skips the initializer.</summary>
    private readonly IReadOnlyList<QueryFilterOption>? values;

    public IReadOnlyList<QueryFilterOption> Values
    {
        get => values ?? [];
        init => values = value;
    }

    /// <summary>Lets a drop-down pick several values even for operators that take one.</summary>
    public bool Multiple { get; init; }

    /// <summary>
    /// Lets one text input hold a list for operators such as <c>in</c>, e.g. <c>;</c>.
    /// Without it those operators still accept a JSON array.
    /// </summary>
    public string? ValueSeparator { get; init; }

    public string? Placeholder { get; init; }

    /// <summary>A heading the filter is listed under.</summary>
    public string? Group { get; init; }

    /// <summary>
    /// Keeps the field for rules that already use it and for SQL, but leaves it out of the field
    /// list. A runner must not be able to pick a column the designer hid.
    /// </summary>
    public bool HideFromPicker { get; init; }

    /// <summary>Help text shown beside the rule.</summary>
    public string? Description { get; init; }

    public UniqueScope Unique { get; init; }

    /// <summary>Stacks radio and checkbox choices instead of laying them out in a row.</summary>
    public bool Vertical { get; init; }

    /// <summary>The height of a textarea input.</summary>
    public int? Rows { get; init; }

    private readonly QueryFilterValidation? validation;

    public QueryFilterValidation Validation
    {
        get => validation ?? QueryFilterValidation.None;
        init => validation = value;
    }

    /// <summary>Copied into the <c>data</c> of rules on this filter, for your own use.</summary>
    public JsonObject? Data { get; init; }

    [JsonIgnore]
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Id : Label;

    [JsonIgnore]
    public QueryInputType EffectiveInput => Input ?? (Values.Count > 0 && Type != QueryValueType.Boolean ? QueryInputType.Select : Type.DefaultInput);
}

[JsonConverter(typeof(QueryFilterOptionJsonConverter))]
public sealed record QueryFilterOption(string Value, string Label, string? Group = null);

/// <summary>
/// Value checks. <see cref="Min"/> and <see cref="Max"/> are lengths for text, bounds for numbers,
/// and ISO dates or times for date types.
/// </summary>
public sealed record QueryFilterValidation
{
    public static QueryFilterValidation None { get; } = new();

    public string? Min { get; init; }

    public string? Max { get; init; }

    public double? Step { get; init; }

    /// <summary>A regular expression text values must match.</summary>
    public string? Format { get; init; }

    public bool AllowEmptyValue { get; init; }

    /// <summary>Replaces the message for an error code, e.g. <c>string_exceed_max_length</c>.</summary>
    public IReadOnlyDictionary<string, string>? Messages { get; init; }
}

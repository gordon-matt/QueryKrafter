namespace QueryKrafter.Models;

/// <summary>
/// An operator a rule can use. The built-in ones are in <see cref="QueryOperators"/>. Add your own
/// to <see cref="QueryBuilderConfig.Operators"/>, with a label (or an <c>operators.{type}</c>
/// translation) and, for SQL or LINQ, a translation in the generator's options.
/// </summary>
public sealed record QueryOperator
{
    /// <summary>The identifier stored in a rule's <c>operator</c>, e.g. <c>equal</c>.</summary>
    public required string Type { get; init; }

    /// <summary>How many inputs the rule shows: 0 for <c>is_null</c>, 2 for <c>between</c>.</summary>
    public int InputCount { get; init; } = 1;

    /// <summary>Each input takes a list of values, as <c>in</c> does.</summary>
    public bool Multiple { get; init; }

    /// <summary>
    /// Gets or sets the applies to.
    /// </summary>
    public QueryValueKind AppliesTo { get; init; } = QueryValueKind.All;

    /// <summary>Shown when the language catalog has no <c>operators.{type}</c> entry.</summary>
    public string? Label { get; init; }

    /// <summary>Groups operators under a heading in the operator list.</summary>
    public string? Group { get; init; }

    /// <summary>The operator that matches exactly what this one doesn't, used to invert a query.</summary>
    public string? Opposite { get; init; }
}

/// <summary>
/// Provides query operators.
/// </summary>
public static class QueryOperators
{
    /// <summary>
    /// The equal constant.
    /// </summary>
    public const string Equal = "equal";
    /// <summary>
    /// The not equal constant.
    /// </summary>
    public const string NotEqual = "not_equal";
    /// <summary>
    /// The in constant.
    /// </summary>
    public const string In = "in";
    /// <summary>
    /// The not in constant.
    /// </summary>
    public const string NotIn = "not_in";
    /// <summary>
    /// The less constant.
    /// </summary>
    public const string Less = "less";
    /// <summary>
    /// The less or equal constant.
    /// </summary>
    public const string LessOrEqual = "less_or_equal";
    /// <summary>
    /// The greater constant.
    /// </summary>
    public const string Greater = "greater";
    /// <summary>
    /// The greater or equal constant.
    /// </summary>
    public const string GreaterOrEqual = "greater_or_equal";
    /// <summary>
    /// The between constant.
    /// </summary>
    public const string Between = "between";
    /// <summary>
    /// The not between constant.
    /// </summary>
    public const string NotBetween = "not_between";
    /// <summary>
    /// The begins with constant.
    /// </summary>
    public const string BeginsWith = "begins_with";
    /// <summary>
    /// The not begins with constant.
    /// </summary>
    public const string NotBeginsWith = "not_begins_with";
    /// <summary>
    /// The contains constant.
    /// </summary>
    public const string Contains = "contains";
    /// <summary>
    /// The not contains constant.
    /// </summary>
    public const string NotContains = "not_contains";
    /// <summary>
    /// The ends with constant.
    /// </summary>
    public const string EndsWith = "ends_with";
    /// <summary>
    /// The not ends with constant.
    /// </summary>
    public const string NotEndsWith = "not_ends_with";
    /// <summary>
    /// The is empty constant.
    /// </summary>
    public const string IsEmpty = "is_empty";
    /// <summary>
    /// The is not empty constant.
    /// </summary>
    public const string IsNotEmpty = "is_not_empty";
    /// <summary>
    /// The is null constant.
    /// </summary>
    public const string IsNull = "is_null";
    /// <summary>
    /// The is not null constant.
    /// </summary>
    public const string IsNotNull = "is_not_null";

    private const QueryValueKind Any = QueryValueKind.All;
    private const QueryValueKind Ordered = QueryValueKind.Number | QueryValueKind.DateTime;
    private const QueryValueKind Listable = QueryValueKind.String | QueryValueKind.Number | QueryValueKind.DateTime;
    private const QueryValueKind Text = QueryValueKind.String;

    /// <summary>
    /// Gets the defaults.
    /// </summary>
    public static IReadOnlyList<QueryOperator> Defaults { get; } =
    [
        new() { Type = Equal, AppliesTo = Any, Opposite = NotEqual },
        new() { Type = NotEqual, AppliesTo = Any, Opposite = Equal },
        new() { Type = In, Multiple = true, AppliesTo = Listable, Opposite = NotIn },
        new() { Type = NotIn, Multiple = true, AppliesTo = Listable, Opposite = In },
        new() { Type = Less, AppliesTo = Ordered, Opposite = GreaterOrEqual },
        new() { Type = LessOrEqual, AppliesTo = Ordered, Opposite = Greater },
        new() { Type = Greater, AppliesTo = Ordered, Opposite = LessOrEqual },
        new() { Type = GreaterOrEqual, AppliesTo = Ordered, Opposite = Less },
        new() { Type = Between, InputCount = 2, AppliesTo = Ordered, Opposite = NotBetween },
        new() { Type = NotBetween, InputCount = 2, AppliesTo = Ordered, Opposite = Between },
        new() { Type = BeginsWith, AppliesTo = Text, Opposite = NotBeginsWith },
        new() { Type = NotBeginsWith, AppliesTo = Text, Opposite = BeginsWith },
        new() { Type = Contains, AppliesTo = Text, Opposite = NotContains },
        new() { Type = NotContains, AppliesTo = Text, Opposite = Contains },
        new() { Type = EndsWith, AppliesTo = Text, Opposite = NotEndsWith },
        new() { Type = NotEndsWith, AppliesTo = Text, Opposite = EndsWith },
        new() { Type = IsEmpty, InputCount = 0, AppliesTo = Text, Opposite = IsNotEmpty },
        new() { Type = IsNotEmpty, InputCount = 0, AppliesTo = Text, Opposite = IsEmpty },
        new() { Type = IsNull, InputCount = 0, AppliesTo = Any, Opposite = IsNotNull },
        new() { Type = IsNotNull, InputCount = 0, AppliesTo = Any, Opposite = IsNull },
    ];

    /// <summary>Text operators that suit long text, where exact matches are rarely useful.</summary>
    public static IReadOnlyList<string> LongText { get; } =
        [BeginsWith, NotBeginsWith, Contains, NotContains, EndsWith, NotEndsWith, IsEmpty, IsNotEmpty, IsNull, IsNotNull];

    /// <summary>Operators that suit a pick list, such as an enumeration.</summary>
    public static IReadOnlyList<string> PickList { get; } = [Equal, NotEqual, In, NotIn, IsNull, IsNotNull];
}

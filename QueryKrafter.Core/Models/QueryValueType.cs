namespace QueryKrafter.Models;

/// <summary>The type of a filter's values. The JSON names match jQuery QueryBuilder's.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QueryValueType>))]
public enum QueryValueType
{
    /// <summary>
    /// The string value.
    /// </summary>
    [JsonStringEnumMemberName("string")]
    String,

    /// <summary>
    /// The integer value.
    /// </summary>
    [JsonStringEnumMemberName("integer")]
    Integer,

    /// <summary>
    /// The double value.
    /// </summary>
    [JsonStringEnumMemberName("double")]
    Double,

    /// <summary>
    /// The date value.
    /// </summary>
    [JsonStringEnumMemberName("date")]
    Date,

    /// <summary>
    /// The time value.
    /// </summary>
    [JsonStringEnumMemberName("time")]
    Time,

    /// <summary>
    /// The date time value.
    /// </summary>
    [JsonStringEnumMemberName("datetime")]
    DateTime,

    /// <summary>
    /// The boolean value.
    /// </summary>
    [JsonStringEnumMemberName("boolean")]
    Boolean,
}

/// <summary>The families of value types an operator can apply to.</summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<QueryValueKind>))]
public enum QueryValueKind
{
    /// <summary>
    /// The none value.
    /// </summary>
    [JsonStringEnumMemberName("none")]
    None = 0,

    /// <summary>
    /// The string value.
    /// </summary>
    [JsonStringEnumMemberName("string")]
    String = 1,

    /// <summary>
    /// The number value.
    /// </summary>
    [JsonStringEnumMemberName("number")]
    Number = 2,

    /// <summary>
    /// The date time value.
    /// </summary>
    [JsonStringEnumMemberName("datetime")]
    DateTime = 4,

    /// <summary>
    /// The boolean value.
    /// </summary>
    [JsonStringEnumMemberName("boolean")]
    Boolean = 8,

    /// <summary>
    /// The all value.
    /// </summary>
    [JsonStringEnumMemberName("all")]
    All = String | Number | DateTime | Boolean,
}

/// <summary>How a rule's value is entered.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QueryInputType>))]
public enum QueryInputType
{
    /// <summary>
    /// The text value.
    /// </summary>
    [JsonStringEnumMemberName("text")]
    Text,

    /// <summary>
    /// The number value.
    /// </summary>
    [JsonStringEnumMemberName("number")]
    Number,

    /// <summary>
    /// The text area value.
    /// </summary>
    [JsonStringEnumMemberName("textarea")]
    TextArea,

    /// <summary>
    /// The radio value.
    /// </summary>
    [JsonStringEnumMemberName("radio")]
    Radio,

    /// <summary>
    /// The checkbox value.
    /// </summary>
    [JsonStringEnumMemberName("checkbox")]
    Checkbox,

    /// <summary>
    /// The select value.
    /// </summary>
    [JsonStringEnumMemberName("select")]
    Select,

    /// <summary>
    /// The date value.
    /// </summary>
    [JsonStringEnumMemberName("date")]
    Date,

    /// <summary>
    /// The time value.
    /// </summary>
    [JsonStringEnumMemberName("time")]
    Time,

    /// <summary>
    /// The date time value.
    /// </summary>
    [JsonStringEnumMemberName("datetime")]
    DateTime,
}

/// <summary>Where a filter marked unique may appear only once.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UniqueScope>))]
public enum UniqueScope
{
    /// <summary>
    /// The none value.
    /// </summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Once in the whole query.</summary>
    [JsonStringEnumMemberName("query")]
    Query,

    /// <summary>Once in each group.</summary>
    [JsonStringEnumMemberName("group")]
    Group,
}

/// <summary>
/// Provides extension methods for query value types.
/// </summary>
public static class QueryValueTypeExtensions
{
    extension(QueryValueType type)
    {
        /// <summary>
        /// Gets the kind.
        /// </summary>
        public QueryValueKind Kind => type switch
        {
            QueryValueType.Integer or QueryValueType.Double => QueryValueKind.Number,
            QueryValueType.Date or QueryValueType.Time or QueryValueType.DateTime => QueryValueKind.DateTime,
            QueryValueType.Boolean => QueryValueKind.Boolean,
            _ => QueryValueKind.String,
        };

        /// <summary>The name jQuery QueryBuilder writes in a rule's <c>type</c>.</summary>
        public string JsonName => type switch
        {
            QueryValueType.Integer => "integer",
            QueryValueType.Double => "double",
            QueryValueType.Date => "date",
            QueryValueType.Time => "time",
            QueryValueType.DateTime => "datetime",
            QueryValueType.Boolean => "boolean",
            _ => "string",
        };

        /// <summary>The input a filter of this type gets when it doesn't choose one.</summary>
        public QueryInputType DefaultInput => type switch
        {
            QueryValueType.Integer or QueryValueType.Double => QueryInputType.Number,
            QueryValueType.Date => QueryInputType.Date,
            QueryValueType.Time => QueryInputType.Time,
            QueryValueType.DateTime => QueryInputType.DateTime,
            QueryValueType.Boolean => QueryInputType.Radio,
            _ => QueryInputType.Text,
        };
    }

    extension(QueryInputType input)
    {
        /// <summary>
        /// Gets the JSON name.
        /// </summary>
        public string JsonName => input switch
        {
            QueryInputType.Number => "number",
            QueryInputType.TextArea => "textarea",
            QueryInputType.Radio => "radio",
            QueryInputType.Checkbox => "checkbox",
            QueryInputType.Select => "select",
            QueryInputType.Date => "date",
            QueryInputType.Time => "time",
            QueryInputType.DateTime => "datetime",
            _ => "text",
        };

        /// <summary>Radio, checkbox and select inputs pick from the filter's values.</summary>
        public bool HasOptions => input is QueryInputType.Radio or QueryInputType.Checkbox or QueryInputType.Select;
    }

    /// <summary>
    /// Parses the value type.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The value type.</returns>
    public static QueryValueType? ParseValueType(string? name) => name?.ToLowerInvariant() switch
    {
        "string" => QueryValueType.String,
        "integer" => QueryValueType.Integer,
        "double" => QueryValueType.Double,
        "date" => QueryValueType.Date,
        "time" => QueryValueType.Time,
        "datetime" => QueryValueType.DateTime,
        "boolean" => QueryValueType.Boolean,
        _ => null,
    };

    /// <summary>
    /// Parses the input type.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The input type.</returns>
    public static QueryInputType? ParseInputType(string? name) => name?.ToLowerInvariant() switch
    {
        "text" => QueryInputType.Text,
        "number" => QueryInputType.Number,
        "textarea" => QueryInputType.TextArea,
        "radio" => QueryInputType.Radio,
        "checkbox" => QueryInputType.Checkbox,
        "select" => QueryInputType.Select,
        "date" => QueryInputType.Date,
        "time" => QueryInputType.Time,
        "datetime" => QueryInputType.DateTime,
        _ => null,
    };
}

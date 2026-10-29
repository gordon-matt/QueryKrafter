namespace QueryKrafter.Models;

/// <summary>The type of a filter's values. The JSON names match jQuery QueryBuilder's.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QueryValueType>))]
public enum QueryValueType
{
    [JsonStringEnumMemberName("string")]
    String,

    [JsonStringEnumMemberName("integer")]
    Integer,

    [JsonStringEnumMemberName("double")]
    Double,

    [JsonStringEnumMemberName("date")]
    Date,

    [JsonStringEnumMemberName("time")]
    Time,

    [JsonStringEnumMemberName("datetime")]
    DateTime,

    [JsonStringEnumMemberName("boolean")]
    Boolean,
}

/// <summary>The families of value types an operator can apply to.</summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<QueryValueKind>))]
public enum QueryValueKind
{
    [JsonStringEnumMemberName("none")]
    None = 0,

    [JsonStringEnumMemberName("string")]
    String = 1,

    [JsonStringEnumMemberName("number")]
    Number = 2,

    [JsonStringEnumMemberName("datetime")]
    DateTime = 4,

    [JsonStringEnumMemberName("boolean")]
    Boolean = 8,

    [JsonStringEnumMemberName("all")]
    All = String | Number | DateTime | Boolean,
}

/// <summary>How a rule's value is entered.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QueryInputType>))]
public enum QueryInputType
{
    [JsonStringEnumMemberName("text")]
    Text,

    [JsonStringEnumMemberName("number")]
    Number,

    [JsonStringEnumMemberName("textarea")]
    TextArea,

    [JsonStringEnumMemberName("radio")]
    Radio,

    [JsonStringEnumMemberName("checkbox")]
    Checkbox,

    [JsonStringEnumMemberName("select")]
    Select,

    [JsonStringEnumMemberName("date")]
    Date,

    [JsonStringEnumMemberName("time")]
    Time,

    [JsonStringEnumMemberName("datetime")]
    DateTime,
}

/// <summary>Where a filter marked unique may appear only once.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UniqueScope>))]
public enum UniqueScope
{
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Once in the whole query.</summary>
    [JsonStringEnumMemberName("query")]
    Query,

    /// <summary>Once in each group.</summary>
    [JsonStringEnumMemberName("group")]
    Group,
}

public static class QueryValueTypeExtensions
{
    extension(QueryValueType type)
    {
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

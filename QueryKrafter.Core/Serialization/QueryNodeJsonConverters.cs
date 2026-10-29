namespace QueryKrafter.Serialization;

internal sealed class QueryNodeJsonConverter : JsonConverter<QueryNode>
{
    public override QueryNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader);
        return node is JsonObject obj && !obj.ContainsKey("rules")
            ? QueryJson.FromJson(new JsonArray(obj.DeepClone())).Rules[0]
            : QueryJson.FromJson(node);
    }

    public override void Write(Utf8JsonWriter writer, QueryNode value, JsonSerializerOptions options)
    {
        var json = value switch
        {
            QueryGroup group => QueryJson.ToJson(group),
            QueryRule rule => QueryJson.ToJson(rule),
            _ => throw new JsonException($"Unknown query node {value.GetType().Name}."),
        };

        json.WriteTo(writer);
    }
}

internal sealed class QueryGroupJsonConverter : JsonConverter<QueryGroup>
{
    public override QueryGroup Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        QueryJson.FromJson(JsonNode.Parse(ref reader));

    public override void Write(Utf8JsonWriter writer, QueryGroup value, JsonSerializerOptions options) =>
        QueryJson.ToJson(value).WriteTo(writer);
}

internal sealed class QueryRuleJsonConverter : JsonConverter<QueryRule>
{
    public override QueryRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonNode.Parse(ref reader) is JsonObject obj
            ? (QueryRule)QueryJson.FromJson(new JsonArray(obj.DeepClone())).Rules[0]
            : null;

    public override void Write(Utf8JsonWriter writer, QueryRule value, JsonSerializerOptions options) =>
        QueryJson.ToJson(value).WriteTo(writer);
}

/// <summary>Accepts numbers and booleans as option values, as configurations written in JavaScript often have them.</summary>
internal sealed class QueryFilterOptionJsonConverter : JsonConverter<QueryFilterOption>
{
    public override QueryFilterOption? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (JsonNode.Parse(ref reader) is not JsonObject json)
        {
            throw new JsonException("A filter value must be an object with \"value\" and \"label\".");
        }

        string value = Scalar(json["value"]) ?? throw new JsonException("A filter value needs a \"value\".");
        return new QueryFilterOption(value, Scalar(json["label"]) ?? value, Scalar(json["group"]));
    }

    public override void Write(Utf8JsonWriter writer, QueryFilterOption value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("value", value.Value);
        writer.WriteString("label", value.Label);
        if (value.Group is not null)
        {
            writer.WriteString("group", value.Group);
        }

        writer.WriteEndObject();
    }

    private static string? Scalar(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue(out string? text) => text,
        JsonValue v => v.ToJsonString(),
        _ => null,
    };
}

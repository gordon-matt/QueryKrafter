namespace QueryKrafter.Serialization;

/// <summary>Source-generated, so configurations deserialize in trimmed WebAssembly builds.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(QueryBuilderConfig))]
internal sealed partial class QueryKrafterJsonContext : JsonSerializerContext
{
    public static QueryKrafterJsonContext Indented { get; } = new(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    });
}

namespace QueryKrafter.Serialization;

/// <summary>
/// Reads and writes rules in the jQuery QueryBuilder format, so rules saved by that library load
/// here unchanged and the other way round:
/// <code>{ "condition": "AND", "not": false, "rules": [ { "id": "price", "operator": "less", "value": 10 } ] }</code>
/// Hidden rules use the design-mode fork's <c>data.show: false</c>; flags use its snake_case names.
/// </summary>
public static class QueryJson
{
    private const string ShowKey = "show";

    private static readonly JsonSerializerOptions Compact = new();
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string Serialize(QueryGroup group, bool indented = false) =>
        ToJson(group).ToJsonString(indented ? Indented : Compact);

    /// <exception cref="JsonException">The text isn't JSON, or its root isn't a group or a list of rules.</exception>
    public static QueryGroup Parse(string json) => FromJson(JsonNode.Parse(json));

    /// <summary>Like <see cref="Parse"/>, with malformed input as an invalid result. Blank input is an empty group.</summary>
    public static Result<QueryGroup> TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new QueryGroup();
        }

        try
        {
            return Parse(json);
        }
        catch (JsonException ex)
        {
            return Result.Invalid(new ValidationError("rules", ex.Message, "invalid_json", ValidationSeverity.Error));
        }
    }

    /// <summary>A root array is read as an AND group of those rules, as jQuery QueryBuilder does.</summary>
    public static QueryGroup FromJson(JsonNode? node) => node switch
    {
        JsonArray array => new QueryGroup { Rules = ReadRules(array) },
        JsonObject obj when obj.ContainsKey("rules") => ReadGroup(obj),
        _ => throw new JsonException("The root of a query must be a group with a \"rules\" list."),
    };

    public static JsonObject ToJson(QueryGroup group)
    {
        var rules = new JsonArray();
        foreach (var node in group.Rules)
        {
            JsonNode? item = node switch
            {
                QueryGroup child => ToJson(child),
                QueryRule rule => ToJson(rule),
                _ => null,
            };
            rules.Add(item);
        }

        var json = new JsonObject
        {
            ["condition"] = group.Condition,
            ["rules"] = rules,
        };

        if (group.Not)
        {
            json["not"] = true;
        }

        if (WriteFlags(group.Flags) is { } flags)
        {
            json["flags"] = flags;
        }

        if (group.Data is { Count: > 0 } data)
        {
            json["data"] = data.DeepClone();
        }

        return json;
    }

    public static JsonObject ToJson(QueryRule rule)
    {
        var json = new JsonObject
        {
            ["id"] = rule.Id,
            ["field"] = rule.Field ?? rule.Id,
            ["operator"] = rule.Operator,
            ["value"] = rule.Value?.DeepClone(),
        };

        if (rule.Type?.JsonName is { } type)
        {
            json["type"] = type;
        }

        if (rule.Input?.JsonName is { } input)
        {
            json["input"] = input;
        }

        var data = rule.Data?.DeepClone().AsObject();
        if (rule.Hidden)
        {
            data ??= [];
            data[ShowKey] = false;
        }

        if (data is { Count: > 0 })
        {
            json["data"] = data;
        }

        if (WriteFlags(rule.Flags) is { } flags)
        {
            json["flags"] = flags;
        }

        return json;
    }

    public static QueryBuilderConfig ParseConfig(string json) =>
        JsonSerializer.Deserialize(json, QueryKrafterJsonContext.Default.QueryBuilderConfig)
            ?? throw new JsonException("The configuration is empty.");

    public static string SerializeConfig(QueryBuilderConfig config, bool indented = false) =>
        JsonSerializer.Serialize(config, indented ? QueryKrafterJsonContext.Indented.QueryBuilderConfig : QueryKrafterJsonContext.Default.QueryBuilderConfig);

    private static List<QueryNode> ReadRules(JsonArray array)
    {
        var rules = new List<QueryNode>(array.Count);
        foreach (var item in array)
        {
            if (item is JsonObject obj)
            {
                rules.Add(obj.ContainsKey("rules") ? ReadGroup(obj) : ReadRule(obj));
            }
        }

        return rules;
    }

    private static QueryGroup ReadGroup(JsonObject json)
    {
        var group = new QueryGroup
        {
            Condition = Text(json["condition"]) is { Length: > 0 } condition ? condition.ToUpperInvariant() : Constants.Conditions.And,
            Not = Flag(json, "not"),
            Rules = json["rules"] is JsonArray rules ? ReadRules(rules) : [],
            Data = json["data"] is JsonObject data && data.Count > 0 ? data.DeepClone().AsObject() : null,
        };

        group.Flags = Flag(json, "readonly")
            ? GroupFlags.ReadOnly
            : json["flags"] is JsonObject flags
                ? new GroupFlags
                {
                    ConditionReadOnly = Flag(flags, "condition_readonly"),
                    NoAddRule = Flag(flags, "no_add_rule"),
                    NoAddGroup = Flag(flags, "no_add_group"),
                    NoDelete = Flag(flags, "no_delete"),
                }
                : GroupFlags.None;

        return group;
    }

    private static QueryRule ReadRule(JsonObject json)
    {
        bool isEmpty = Flag(json, "empty");

        var rule = new QueryRule
        {
            Id = isEmpty ? null : Text(json["id"]),
            Field = isEmpty ? null : Text(json["field"]),
            Type = QueryValueTypeExtensions.ParseValueType(Text(json["type"])),
            Input = QueryValueTypeExtensions.ParseInputType(Text(json["input"])),
            Operator = Text(json["operator"]),
            Value = json["value"]?.DeepClone(),
        };

        if (json["data"] is JsonObject data)
        {
            var copy = data.DeepClone().AsObject();
            if (copy[ShowKey] is JsonValue show && show.TryGetValue(out bool visible))
            {
                rule.Hidden = !visible;
                copy.Remove(ShowKey);
            }

            rule.Data = copy.Count > 0 ? copy : null;
        }

        rule.Flags = Flag(json, "readonly")
            ? RuleFlags.ReadOnly
            : json["flags"] is JsonObject flags
                ? new RuleFlags
                {
                    FilterReadOnly = Flag(flags, "filter_readonly"),
                    OperatorReadOnly = Flag(flags, "operator_readonly"),
                    ValueReadOnly = Flag(flags, "value_readonly"),
                    NoDelete = Flag(flags, "no_delete"),
                }
                : RuleFlags.None;

        return rule;
    }

    private static JsonObject? WriteFlags(RuleFlags flags)
    {
        if (flags.IsEmpty)
        {
            return null;
        }

        var json = new JsonObject();
        AddFlag(json, "filter_readonly", flags.FilterReadOnly);
        AddFlag(json, "operator_readonly", flags.OperatorReadOnly);
        AddFlag(json, "value_readonly", flags.ValueReadOnly);
        AddFlag(json, "no_delete", flags.NoDelete);
        return json;
    }

    private static JsonObject? WriteFlags(GroupFlags flags)
    {
        if (flags.IsEmpty)
        {
            return null;
        }

        var json = new JsonObject();
        AddFlag(json, "condition_readonly", flags.ConditionReadOnly);
        AddFlag(json, "no_add_rule", flags.NoAddRule);
        AddFlag(json, "no_add_group", flags.NoAddGroup);
        AddFlag(json, "no_delete", flags.NoDelete);
        return json;
    }

    private static void AddFlag(JsonObject json, string name, bool value)
    {
        if (value)
        {
            json[name] = true;
        }
    }

    private static bool Flag(JsonObject json, string name) =>
        json[name] is JsonValue value && value.TryGetValue(out bool flag) && flag;

    /// <summary>Strings as they are, and numbers or booleans as their JSON text, since ids are sometimes numeric.</summary>
    private static string? Text(JsonNode? node) => node switch
    {
        JsonValue value when value.TryGetValue(out string? text) => text,
        JsonValue value => value.ToJsonString(),
        _ => null,
    };
}

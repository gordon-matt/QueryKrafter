namespace QueryKrafter.Values;

/// <summary>
/// Reads and writes <see cref="QueryRule.Value"/> by input. An operator with one input stores a
/// value, or a list when it takes several; one with more inputs stores <c>[first, second]</c>.
/// </summary>
public static class RuleValues
{
    /// <summary>The values in each of the operator's inputs: one for most, several for a list.</summary>
    public static IReadOnlyList<IReadOnlyList<JsonNode?>> Slots(QueryRule rule, QueryFilter filter, QueryOperator op)
    {
        if (op.InputCount <= 0)
        {
            return [];
        }

        if (op.InputCount == 1)
        {
            return [Expand(rule.Value, filter, op)];
        }

        var slots = new List<IReadOnlyList<JsonNode?>>(op.InputCount);
        for (int i = 0; i < op.InputCount; i++)
        {
            var node = rule.Value switch
            {
                JsonArray array => i < array.Count ? array[i] : null,
                var single when i == 0 => single,
                _ => null,
            };

            slots.Add(Expand(node, filter, op));
        }

        return slots;
    }

    /// <summary>Every non-empty value as a CLR value, in input order. Call after validation: values that don't parse are skipped.</summary>
    public static IReadOnlyList<object?> Typed(QueryRule rule, QueryFilter filter, QueryOperator op)
    {
        var values = new List<object?>();
        foreach (var slot in Slots(rule, filter, op))
        {
            foreach (var node in slot)
            {
                if (QueryValueParser.TryParse(node, filter.Type, out object? value))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    /// <summary>The input's values as text, for list editors such as checkboxes and multi-selects.</summary>
    public static IReadOnlyList<string> GetTexts(QueryRule rule, QueryFilter filter, QueryOperator op, int input)
    {
        var slots = Slots(rule, filter, op);
        return input < slots.Count
            ? [.. slots[input].OfType<JsonValue>().Select(QueryValueParser.ToText)]
            : [];
    }

    /// <summary>The input's value as one text, joined with the filter's separator for list operators.</summary>
    public static string? GetText(QueryRule rule, QueryFilter filter, QueryOperator op, int input)
    {
        var texts = GetTexts(rule, filter, op, input);
        if (texts.Count == 0)
        {
            return null;
        }

        return texts.Count == 1 || string.IsNullOrEmpty(filter.ValueSeparator)
            ? texts[0]
            : string.Join(filter.ValueSeparator, texts);
    }

    /// <summary>Sets one input from text, splitting on the filter's separator for list operators.</summary>
    public static void SetText(QueryRule rule, QueryFilter filter, QueryOperator op, int input, string? text)
    {
        IReadOnlyList<string?> texts = op.Multiple && !string.IsNullOrEmpty(filter.ValueSeparator) && text is not null
            ? [.. text.Split(filter.ValueSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]
            : [text];

        SetTexts(rule, filter, op, input, texts);
    }

    /// <summary>Sets one input from a list of texts, typed by the filter.</summary>
    public static void SetTexts(QueryRule rule, QueryFilter filter, QueryOperator op, int input, IReadOnlyList<string?> texts)
    {
        bool isList = op.Multiple || filter.Multiple || filter.EffectiveInput == QueryInputType.Checkbox;

        JsonNode? node = isList
            ? new JsonArray([.. texts.Where(t => !string.IsNullOrEmpty(t)).Select(t => ToNode(filter, t))])
            : ToNode(filter, texts.Count > 0 ? texts[0] : null);

        SetNode(rule, op, input, node);
    }

    public static void SetNode(QueryRule rule, QueryOperator op, int input, JsonNode? node)
    {
        if (op.InputCount <= 1)
        {
            rule.Value = node;
            return;
        }

        var current = rule.Value as JsonArray;
        var array = new JsonArray();
        for (int i = 0; i < op.InputCount; i++)
        {
            array.Add(i == input ? node : current is not null && i < current.Count ? current[i]?.DeepClone() : null);
        }

        rule.Value = array;
    }

    /// <summary>
    /// Types text as the filter's JSON value: numbers and booleans when they parse, text otherwise,
    /// so a half-typed number stays visible and fails validation rather than vanishing.
    /// </summary>
    public static JsonNode? ToNode(QueryFilter filter, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var raw = JsonValue.Create(text);
        return filter.Type switch
        {
            QueryValueType.Integer when QueryValueParser.TryParse(raw, QueryValueType.Integer, out object? whole) => JsonValue.Create((long)whole!),
            QueryValueType.Double when QueryValueParser.TryParse(raw, QueryValueType.Double, out object? real) && real is decimal number => JsonValue.Create(number),
            QueryValueType.Boolean when QueryValueParser.TryParse(raw, QueryValueType.Boolean, out object? flag) => JsonValue.Create((bool)flag!),
            _ => raw,
        };
    }

    /// <summary>Whether a rule's value has the shape <paramref name="op"/> expects, so it can be kept when the operator changes.</summary>
    public static bool Fits(QueryOperator? previous, QueryOperator next) =>
        previous is not null && previous.InputCount == next.InputCount && previous.Multiple == next.Multiple;

    private static IReadOnlyList<JsonNode?> Expand(JsonNode? node, QueryFilter filter, QueryOperator op) => node switch
    {
        JsonArray array => [.. array],
        JsonValue value when op.Multiple && !string.IsNullOrEmpty(filter.ValueSeparator) && value.TryGetValue(out string? text) =>
            [.. text.Split(filter.ValueSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(t => (JsonNode?)JsonValue.Create(t))],
        null => [],
        _ => [node],
    };
}

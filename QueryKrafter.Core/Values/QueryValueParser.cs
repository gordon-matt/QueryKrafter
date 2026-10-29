namespace QueryKrafter.Values;

/// <summary>
/// Turns JSON values into CLR values: <see cref="string"/>, <see cref="long"/>, <see cref="decimal"/>
/// (or <see cref="double"/> when out of range), <see cref="DateOnly"/>, <see cref="TimeOnly"/>,
/// <see cref="DateTime"/> and <see cref="bool"/>. Text is always read with the invariant culture,
/// since browsers send ISO dates and dot decimals whatever the user's language.
/// </summary>
public static class QueryValueParser
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-M-d"];
    private static readonly string[] TimeFormats = ["HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF", "H:mm", "H:mm:ss"];

    public static bool IsEmpty(JsonNode? node) => node switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonValue value => value.TryGetValue(out string? text) && text.Length == 0,
        _ => false,
    };

    public static bool TryParse(JsonNode? node, QueryValueType type, out object? value)
    {
        value = null;
        if (node is not JsonValue json || IsEmpty(node))
        {
            return false;
        }

        switch (type)
        {
            case QueryValueType.String:
                value = ToText(json);
                return true;

            case QueryValueType.Integer:
                if (TryDecimal(json, out decimal number) && decimal.Truncate(number) == number && number is >= long.MinValue and <= long.MaxValue)
                {
                    value = (long)number;
                    return true;
                }

                return false;

            case QueryValueType.Double:
                if (TryDecimal(json, out decimal real))
                {
                    value = real;
                    return true;
                }

                if (TryDouble(json, out double large))
                {
                    value = large;
                    return true;
                }

                return false;

            case QueryValueType.Date:
                if (TryDate(ToText(json), out var date))
                {
                    value = date;
                    return true;
                }

                return false;

            case QueryValueType.Time:
                if (TryTime(ToText(json), out var time))
                {
                    value = time;
                    return true;
                }

                return false;

            case QueryValueType.DateTime:
                if (TryDateTime(ToText(json), out var dateTime))
                {
                    value = dateTime;
                    return true;
                }

                return false;

            case QueryValueType.Boolean:
                if (TryBoolean(json, out bool flag))
                {
                    value = flag;
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    /// <summary>Whether the value is a number at all, to tell "not a number" from "not an integer".</summary>
    internal static bool IsNumber(JsonValue json) => TryDecimal(json, out _) || TryDouble(json, out _);

    public static string ToText(JsonValue json)
    {
        if (json.TryGetValue(out string? text))
        {
            return text;
        }

        if (json.TryGetValue(out bool flag))
        {
            return flag ? "true" : "false";
        }

        return json.ToJsonString();
    }

    public static bool TryDate(string? text, out DateOnly date)
    {
        if (DateOnly.TryParseExact(text?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (TryDateTime(text, out var dateTime))
        {
            date = DateOnly.FromDateTime(dateTime);
            return true;
        }

        return false;
    }

    public static bool TryTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text?.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static bool TryDateTime(string? text, out DateTime dateTime) =>
        DateTime.TryParse(text?.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out dateTime);

    /// <summary>
    /// Numbers are read from their JSON text, since a value made from a CLR <see cref="int"/> won't
    /// convert to <see cref="decimal"/> through <see cref="JsonValue.TryGetValue{T}"/>, unlike a parsed one.
    /// </summary>
    private static string? NumberText(JsonValue json) => json.GetValueKind() switch
    {
        JsonValueKind.Number => json.ToJsonString(),
        JsonValueKind.String => json.GetValue<string>().Trim(),
        _ => null,
    };

    private static bool TryDecimal(JsonValue json, out decimal number)
    {
        number = 0;
        return NumberText(json) is { } text
            && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    private static bool TryDouble(JsonValue json, out double number)
    {
        number = 0;
        return NumberText(json) is { } text
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            && double.IsFinite(number);
    }

    private static bool TryBoolean(JsonValue json, out bool flag)
    {
        if (json.TryGetValue(out flag))
        {
            return true;
        }

        if (json.GetValueKind() == JsonValueKind.Number && TryDecimal(json, out decimal number) && number is 0 or 1)
        {
            flag = number == 1;
            return true;
        }

        switch (json.TryGetValue(out string? text) ? text.Trim().ToLowerInvariant() : null)
        {
            case "true" or "1":
                flag = true;
                return true;
            case "false" or "0":
                flag = false;
                return true;
            default:
                return false;
        }
    }
}

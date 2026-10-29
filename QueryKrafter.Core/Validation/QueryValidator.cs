using System.Text.RegularExpressions;
using QueryKrafter.Values;
using Codes = QueryKrafter.Validation.QueryErrorCodes;

namespace QueryKrafter.Validation;

/// <summary>
/// Checks rules against a configuration: chosen filters and operators, values for each input, and
/// the group rules (conditions, nesting and empty groups). Hidden rules are checked like any other,
/// since they still take part in the query.
/// </summary>
public static class QueryValidator
{
    private static readonly TimeSpan FormatTimeout = TimeSpan.FromMilliseconds(250);

    /// <param name="skipEmpty">Ignore rules with no filter chosen, and groups left empty by that, instead of reporting them.</param>
    /// <param name="text">The language for operator names in messages. Defaults to English.</param>
    public static IReadOnlyList<QueryError> Validate(QueryGroup root, QueryBuilderConfig config, bool skipEmpty = false, QueryKrafterText? text = null)
    {
        var errors = new List<QueryError>();
        var context = new Context(config, skipEmpty, text ?? QueryKrafterText.English, errors);
        ValidateGroup(root, string.Empty, level: 1, context);
        return errors;
    }

    /// <summary>The errors as an invalid result, keyed by <see cref="QueryError.Path"/>, with English messages.</summary>
    public static Result ToResult(IReadOnlyList<QueryError> errors, QueryBuilderConfig config, QueryKrafterText? text = null)
    {
        if (errors.Count == 0)
        {
            return Result.Success();
        }

        var language = text ?? QueryKrafterText.English;
        return Result.Invalid([.. errors.Select(e => new ValidationError(
            e.Path,
            e.Message(language, e.Node is QueryRule rule ? config.FindFilter(rule.Id)?.Validation : null),
            e.Code,
            ValidationSeverity.Error))]);
    }

    /// <returns><c>true</c> when the group counts as a non-empty part of the query.</returns>
    private static bool ValidateGroup(QueryGroup group, string path, int level, Context context)
    {
        var config = context.Config;

        if (!config.Conditions.Contains(group.Condition, StringComparer.OrdinalIgnoreCase))
        {
            context.Add(group, path, Codes.InvalidCondition, group.Condition);
        }

        if (level > 1 && !config.CanAddGroupAt(level - 1))
        {
            context.Add(group, path, Codes.TooManyGroups, config.MaxGroupDepth.ToString(CultureInfo.InvariantCulture));
        }

        int counted = 0;
        var usedInGroup = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < group.Rules.Count; i++)
        {
            string childPath = path.Length == 0 ? i.ToString(CultureInfo.InvariantCulture) : $"{path}.{i}";

            switch (group.Rules[i])
            {
                case QueryGroup child:
                    if (ValidateGroup(child, childPath, level + 1, context))
                    {
                        counted++;
                    }

                    break;

                case QueryRule rule:
                    if (ValidateRule(rule, childPath, usedInGroup, context))
                    {
                        counted++;
                    }

                    break;
            }
        }

        if (counted > 0)
        {
            return true;
        }

        bool isRoot = level == 1;
        if (!isRoot && context.SkipEmpty)
        {
            return false;
        }

        if (!isRoot || !config.AllowEmpty)
        {
            context.Add(group, path, Codes.EmptyGroup);
        }

        return false;
    }

    private static bool ValidateRule(QueryRule rule, string path, HashSet<string> usedInGroup, Context context)
    {
        var config = context.Config;

        if (string.IsNullOrEmpty(rule.Id))
        {
            if (context.SkipEmpty)
            {
                return false;
            }

            context.Add(rule, path, Codes.NoFilter);
            return true;
        }

        if (config.FindFilter(rule.Id) is not { } filter)
        {
            context.Add(rule, path, Codes.UnknownFilter, rule.Id);
            return true;
        }

        bool repeatedInQuery = !context.UsedInQuery.Add(filter.Id);
        bool repeatedInGroup = !usedInGroup.Add(filter.Id);
        if ((filter.Unique == UniqueScope.Query && repeatedInQuery) || (filter.Unique == UniqueScope.Group && repeatedInGroup))
        {
            context.Add(rule, path, Codes.FilterNotUnique, filter.DisplayLabel);
            return true;
        }

        if (string.IsNullOrEmpty(rule.Operator))
        {
            context.Add(rule, path, Codes.NoOperator);
            return true;
        }

        var op = config.OperatorsFor(filter).FirstOrDefault(o => string.Equals(o.Type, rule.Operator, StringComparison.Ordinal));
        if (op is null)
        {
            context.Add(rule, path, Codes.InvalidOperator, rule.Operator);
            return true;
        }

        if (ValidateValues(rule, filter, op, context.Text) is { } error)
        {
            context.Add(rule, path, error.Code, [.. error.Arguments]);
            return true;
        }

        if (config.RuleValidator?.Invoke(rule, filter) is { } custom)
        {
            context.Add(rule, path, custom.Code, [.. custom.Arguments]);
        }

        return true;
    }

    private static QueryRuleError? ValidateValues(QueryRule rule, QueryFilter filter, QueryOperator op, QueryKrafterText text)
    {
        var validation = filter.Validation;
        var slots = RuleValues.Slots(rule, filter, op);

        foreach (var slot in slots)
        {
            if (!op.Multiple && slot.Count > 1)
            {
                return new(Codes.OperatorNotMultiple, text.OperatorLabel(op));
            }

            var values = slot.Where(v => !QueryValueParser.IsEmpty(v)).ToList();
            var input = filter.EffectiveInput;

            if (input.HasOptions && values.Count == 0)
            {
                if (validation.AllowEmptyValue)
                {
                    continue;
                }

                return new(input switch
                {
                    QueryInputType.Radio => Codes.RadioEmpty,
                    QueryInputType.Checkbox => Codes.CheckboxEmpty,
                    _ => Codes.SelectEmpty,
                });
            }

            if (values.Count == 0)
            {
                if (validation.AllowEmptyValue)
                {
                    continue;
                }

                return new(filter.Type.Kind switch
                {
                    QueryValueKind.Number => Codes.NumberNaN,
                    QueryValueKind.DateTime => Codes.DateTimeEmpty,
                    QueryValueKind.Boolean => Codes.BooleanNotValid,
                    _ => Codes.StringEmpty,
                });
            }

            foreach (var value in values.OfType<JsonValue>())
            {
                if (ValidateValue(value, filter) is { } error)
                {
                    return error;
                }
            }
        }

        if (op.InputCount == 2 && slots.Count == 2 && slots[0].Count == 1 && slots[1].Count == 1
            && QueryValueParser.TryParse(slots[0][0], filter.Type, out object? from)
            && QueryValueParser.TryParse(slots[1][0], filter.Type, out object? to)
            && from is IComparable comparable && to is not null && from.GetType() == to.GetType()
            && comparable.CompareTo(to) > 0)
        {
            return new(
                filter.Type.Kind == QueryValueKind.Number ? Codes.NumberBetweenInvalid : Codes.DateTimeBetweenInvalid,
                Display(slots[0][0]), Display(slots[1][0]));
        }

        return null;
    }

    private static QueryRuleError? ValidateValue(JsonValue json, QueryFilter filter)
    {
        var validation = filter.Validation;

        switch (filter.Type.Kind)
        {
            case QueryValueKind.String:
                string text = QueryValueParser.ToText(json);
                if (Number(validation.Min) is { } minLength && text.Length < minLength)
                {
                    return new(Codes.StringTooShort, validation.Min!);
                }

                if (Number(validation.Max) is { } maxLength && text.Length > maxLength)
                {
                    return new(Codes.StringTooLong, validation.Max!);
                }

                if (!string.IsNullOrEmpty(validation.Format) && !MatchesFormat(text, validation.Format))
                {
                    return new(Codes.StringInvalidFormat, validation.Format);
                }

                return null;

            case QueryValueKind.Number:
                if (!QueryValueParser.IsNumber(json))
                {
                    return new(Codes.NumberNaN);
                }

                if (!QueryValueParser.TryParse(json, filter.Type, out object? parsed))
                {
                    return new(filter.Type == QueryValueType.Integer ? Codes.NumberNotInteger : Codes.NumberNotDouble);
                }

                double number = Convert.ToDouble(parsed, CultureInfo.InvariantCulture);
                if (Number(validation.Min) is { } min && number < min)
                {
                    return new(Codes.NumberTooSmall, validation.Min!);
                }

                if (Number(validation.Max) is { } max && number > max)
                {
                    return new(Codes.NumberTooLarge, validation.Max!);
                }

                if (validation.Step is double step && step > 0 && parsed is decimal or long)
                {
                    decimal exact = Convert.ToDecimal(parsed, CultureInfo.InvariantCulture);
                    decimal stepSize = (decimal)step;
                    if (exact % stepSize != 0)
                    {
                        return new(Codes.NumberWrongStep, step.ToString(CultureInfo.InvariantCulture));
                    }
                }

                return null;

            case QueryValueKind.DateTime:
                if (!QueryValueParser.TryParse(json, filter.Type, out object? moment) || moment is not IComparable when)
                {
                    return new(Codes.DateTimeInvalid, ExpectedFormat(filter.Type));
                }

                if (validation.Min is not null && QueryValueParser.TryParse(JsonValue.Create(validation.Min), filter.Type, out object? earliest) && when.CompareTo(earliest) < 0)
                {
                    return new(Codes.DateTimeTooEarly, validation.Min);
                }

                if (validation.Max is not null && QueryValueParser.TryParse(JsonValue.Create(validation.Max), filter.Type, out object? latest) && when.CompareTo(latest) > 0)
                {
                    return new(Codes.DateTimeTooLate, validation.Max);
                }

                return null;

            case QueryValueKind.Boolean:
                return QueryValueParser.TryParse(json, QueryValueType.Boolean, out _) ? null : new(Codes.BooleanNotValid);

            default:
                return null;
        }
    }

    /// <summary>A timeout, since the pattern may come from configuration written by someone else.</summary>
    private static bool MatchesFormat(string text, string pattern)
    {
        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, FormatTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static string ExpectedFormat(QueryValueType type) => type switch
    {
        QueryValueType.Date => "yyyy-MM-dd",
        QueryValueType.Time => "HH:mm",
        _ => "yyyy-MM-dd HH:mm",
    };

    private static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;

    private static string Display(JsonNode? node) => node is JsonValue value ? QueryValueParser.ToText(value) : string.Empty;

    private sealed record Context(QueryBuilderConfig Config, bool SkipEmpty, QueryKrafterText Text, List<QueryError> Errors)
    {
        public HashSet<string> UsedInQuery { get; } = new(StringComparer.Ordinal);

        public void Add(QueryNode node, string path, string code, params string[] arguments) =>
            Errors.Add(new QueryError(node, path, code, arguments));
    }
}

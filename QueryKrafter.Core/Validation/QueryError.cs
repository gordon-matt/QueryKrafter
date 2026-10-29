namespace QueryKrafter.Validation;

/// <summary>
/// A problem with one rule or group. <see cref="Path"/> locates it by position: empty for the
/// root, <c>0</c> for its first child, <c>0.2</c> for that child's third, and so on.
/// </summary>
public sealed record QueryError(QueryNode Node, string Path, string Code, IReadOnlyList<string> Arguments)
{
    /// <summary>The message in <paramref name="text"/>'s language, or the filter's own message for this code.</summary>
    public string Message(QueryKrafterText text, QueryFilterValidation? validation = null)
    {
        string? custom = validation?.Messages?.GetValueOrDefault(Code);
        if (custom is not null)
        {
            try
            {
                return string.Format(text.Culture, custom, [.. Arguments]);
            }
            catch (FormatException)
            {
                return custom;
            }
        }

        return text.Format("errors." + Code, [.. Arguments]);
    }
}

public static class QueryErrorCodes
{
    public const string NoFilter = "no_filter";
    public const string UnknownFilter = "unknown_filter";
    public const string NoOperator = "no_operator";
    public const string InvalidOperator = "invalid_operator";
    public const string FilterNotUnique = "filter_not_unique";
    public const string EmptyGroup = "empty_group";
    public const string InvalidCondition = "invalid_condition";
    public const string TooManyGroups = "too_many_groups";
    public const string RadioEmpty = "radio_empty";
    public const string CheckboxEmpty = "checkbox_empty";
    public const string SelectEmpty = "select_empty";
    public const string StringEmpty = "string_empty";
    public const string StringTooShort = "string_exceed_min_length";
    public const string StringTooLong = "string_exceed_max_length";
    public const string StringInvalidFormat = "string_invalid_format";
    public const string NumberNaN = "number_nan";
    public const string NumberNotInteger = "number_not_integer";
    public const string NumberNotDouble = "number_not_double";
    public const string NumberTooSmall = "number_exceed_min";
    public const string NumberTooLarge = "number_exceed_max";
    public const string NumberWrongStep = "number_wrong_step";
    public const string NumberBetweenInvalid = "number_between_invalid";
    public const string DateTimeEmpty = "datetime_empty";
    public const string DateTimeInvalid = "datetime_invalid";
    public const string DateTimeTooEarly = "datetime_exceed_min";
    public const string DateTimeTooLate = "datetime_exceed_max";
    public const string DateTimeBetweenInvalid = "datetime_between_invalid";
    public const string BooleanNotValid = "boolean_not_valid";
    public const string OperatorNotMultiple = "operator_not_multiple";
}

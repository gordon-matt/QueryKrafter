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

/// <summary>
/// Provides query error codes.
/// </summary>
public static class QueryErrorCodes
{
    /// <summary>
    /// The no filter constant.
    /// </summary>
    public const string NoFilter = "no_filter";
    /// <summary>
    /// The unknown filter constant.
    /// </summary>
    public const string UnknownFilter = "unknown_filter";
    /// <summary>
    /// The no operator constant.
    /// </summary>
    public const string NoOperator = "no_operator";
    /// <summary>
    /// The invalid operator constant.
    /// </summary>
    public const string InvalidOperator = "invalid_operator";
    /// <summary>
    /// The filter not unique constant.
    /// </summary>
    public const string FilterNotUnique = "filter_not_unique";
    /// <summary>
    /// The empty group constant.
    /// </summary>
    public const string EmptyGroup = "empty_group";
    /// <summary>
    /// The invalid condition constant.
    /// </summary>
    public const string InvalidCondition = "invalid_condition";
    /// <summary>
    /// The too many groups constant.
    /// </summary>
    public const string TooManyGroups = "too_many_groups";
    /// <summary>
    /// The radio empty constant.
    /// </summary>
    public const string RadioEmpty = "radio_empty";
    /// <summary>
    /// The checkbox empty constant.
    /// </summary>
    public const string CheckboxEmpty = "checkbox_empty";
    /// <summary>
    /// The select empty constant.
    /// </summary>
    public const string SelectEmpty = "select_empty";
    /// <summary>
    /// The string empty constant.
    /// </summary>
    public const string StringEmpty = "string_empty";
    /// <summary>
    /// The string too short constant.
    /// </summary>
    public const string StringTooShort = "string_exceed_min_length";
    /// <summary>
    /// The string too long constant.
    /// </summary>
    public const string StringTooLong = "string_exceed_max_length";
    /// <summary>
    /// The string invalid format constant.
    /// </summary>
    public const string StringInvalidFormat = "string_invalid_format";
    /// <summary>
    /// The number na n constant.
    /// </summary>
    public const string NumberNaN = "number_nan";
    /// <summary>
    /// The number not integer constant.
    /// </summary>
    public const string NumberNotInteger = "number_not_integer";
    /// <summary>
    /// The number not double constant.
    /// </summary>
    public const string NumberNotDouble = "number_not_double";
    /// <summary>
    /// The number too small constant.
    /// </summary>
    public const string NumberTooSmall = "number_exceed_min";
    /// <summary>
    /// The number too large constant.
    /// </summary>
    public const string NumberTooLarge = "number_exceed_max";
    /// <summary>
    /// The number wrong step constant.
    /// </summary>
    public const string NumberWrongStep = "number_wrong_step";
    /// <summary>
    /// The number between invalid constant.
    /// </summary>
    public const string NumberBetweenInvalid = "number_between_invalid";
    /// <summary>
    /// The date time empty constant.
    /// </summary>
    public const string DateTimeEmpty = "datetime_empty";
    /// <summary>
    /// The date time invalid constant.
    /// </summary>
    public const string DateTimeInvalid = "datetime_invalid";
    /// <summary>
    /// The date time too early constant.
    /// </summary>
    public const string DateTimeTooEarly = "datetime_exceed_min";
    /// <summary>
    /// The date time too late constant.
    /// </summary>
    public const string DateTimeTooLate = "datetime_exceed_max";
    /// <summary>
    /// The date time between invalid constant.
    /// </summary>
    public const string DateTimeBetweenInvalid = "datetime_between_invalid";
    /// <summary>
    /// The boolean not valid constant.
    /// </summary>
    public const string BooleanNotValid = "boolean_not_valid";
    /// <summary>
    /// The operator not multiple constant.
    /// </summary>
    public const string OperatorNotMultiple = "operator_not_multiple";
}

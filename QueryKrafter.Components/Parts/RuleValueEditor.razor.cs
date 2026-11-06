namespace QueryKrafter.Components.Parts;

/// <summary>
/// Represents the rule value editor.
/// </summary>
public sealed partial class RuleValueEditor
{
    private const string DefaultSeparator = ",";

    private static int instances;

    /// <summary>Unique per editor, so radio buttons in different rules don't share a group.</summary>
    private readonly string id = "qk-value-" + Interlocked.Increment(ref instances).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets or sets the builder.
    /// </summary>
    [CascadingParameter] public QueryBuilder Builder { get; set; } = default!;

    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    [Parameter, EditorRequired] public QueryRule Rule { get; set; } = default!;

    /// <summary>
    /// Gets or sets the filter.
    /// </summary>
    [Parameter, EditorRequired] public QueryFilter Filter { get; set; } = default!;

    /// <summary>
    /// Gets or sets the operator.
    /// </summary>
    [Parameter, EditorRequired] public QueryOperator Operator { get; set; } = default!;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is disabled.
    /// </summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>A list typed into one text box, as <c>in</c> needs, is split on a comma unless the filter names a separator.</summary>
    private bool IsTypedList => Operator.Multiple && !Filter.EffectiveInput.HasOptions;

    private QueryFilter EditFilter => IsTypedList && string.IsNullOrEmpty(Filter.ValueSeparator)
        ? Filter with { ValueSeparator = DefaultSeparator }
        : Filter;

    private bool IsMultiSelect => Operator.Multiple || Filter.Multiple;

    private string HtmlType => IsTypedList ? "text" : Filter.EffectiveInput switch
    {
        QueryInputType.Number => "number",
        QueryInputType.Date => "date",
        QueryInputType.Time => "time",
        QueryInputType.DateTime => "datetime-local",
        _ => "text",
    };

    private string? Placeholder => IsTypedList && string.IsNullOrEmpty(Filter.Placeholder)
        ? Builder.Text.Format("rule.separatorHint", EditFilter.ValueSeparator)
        : Filter.Placeholder;

    private string? Step => HtmlType switch
    {
        "number" => Filter.Validation.Step?.ToString(CultureInfo.InvariantCulture) ?? (Filter.Type == QueryValueType.Integer ? "1" : "any"),
        "time" or "datetime-local" => Filter.Validation.Step?.ToString(CultureInfo.InvariantCulture),
        _ => null,
    };

    /// <summary>The choices for radio, checkbox and select inputs; Yes and No for a boolean without its own.</summary>
    private IReadOnlyList<QueryFilterOption> Options => Filter.Values.Count > 0 || Filter.Type != QueryValueType.Boolean
        ? Filter.Values
        : [new("true", Builder.Text["boolean.true"]), new("false", Builder.Text["boolean.false"])];

    private IReadOnlyList<string> Texts(int input) => RuleValues.GetTexts(Rule, EditFilter, Operator, input);

    /// <summary>The input's text, in the format the browser's date and time inputs expect.</summary>
    private string? Text(int input)
    {
        string? value = RuleValues.GetText(Rule, EditFilter, Operator, input);
        if (value is null || IsTypedList)
        {
            return value;
        }

        return HtmlType switch
        {
            "datetime-local" when QueryValueParser.TryDateTime(value, out var dateTime) => dateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            "date" when QueryValueParser.TryDate(value, out var date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => value,
        };
    }

    /// <summary>Number and date bounds only; for text, <see cref="QueryFilterValidation.Min"/> and Max are lengths.</summary>
    private string? Bound(string? value) => HtmlType is "number" or "date" or "time" or "datetime-local" ? value : null;

    private Task SetTextAsync(int input, string? text)
    {
        RuleValues.SetText(Rule, EditFilter, Operator, input, text);
        return Builder.ValueChangedAsync(Rule);
    }

    private Task SetTextsAsync(int input, IReadOnlyList<string?> texts)
    {
        RuleValues.SetTexts(Rule, EditFilter, Operator, input, texts);
        return Builder.ValueChangedAsync(Rule);
    }

    private Task ToggleAsync(int input, string value, bool isChecked)
    {
        var texts = Texts(input).Where(t => !string.Equals(t, value, StringComparison.Ordinal)).ToList();
        if (isChecked)
        {
            texts.Add(value);
        }

        // Keep the filter's order rather than the order of clicks.
        var ordered = Options.Select(o => o.Value).Where(texts.Contains).ToList<string?>();
        return SetTextsAsync(input, ordered);
    }

    /// <summary>A multiple select reports every chosen value as a string array.</summary>
    private Task OnSelectChangedAsync(int input, ChangeEventArgs change) => change.Value switch
    {
        string[] values => SetTextsAsync(input, values),
        string value => SetTextAsync(input, value),
        _ => SetTextAsync(input, null),
    };
}

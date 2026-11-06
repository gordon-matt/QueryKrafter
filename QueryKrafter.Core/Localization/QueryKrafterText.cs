namespace QueryKrafter.Localization;

/// <summary>
/// The strings for one language. The indexer returns English when this catalog has no value for
/// a key, and the key itself only when English has no value either.
/// </summary>
public sealed class QueryKrafterText
{
    /// <summary>
    /// Gets the english.
    /// </summary>
    public static QueryKrafterText English { get; } = QueryKrafterLocaleCatalog.Shared.Resolve("en");

    private readonly IReadOnlyList<Dictionary<string, string>> chain;

    internal QueryKrafterText(string language, CultureInfo culture, bool isRightToLeft, IReadOnlyList<Dictionary<string, string>> chain)
    {
        Language = language;
        Culture = culture;
        IsRightToLeft = isRightToLeft;
        this.chain = chain;
    }

    /// <summary>
    /// Gets the language.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Gets the culture.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is right to left.
    /// </summary>
    public bool IsRightToLeft { get; }

    /// <summary>
    /// Gets or sets the item at the specified index.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The item at the specified index.</returns>
    public string this[string key] => Get(key);

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The string.</returns>
    public string Get(string key) => Find(key) ?? key;

    /// <summary>
    /// Formats the value.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="args">The args.</param>
    /// <returns>The string.</returns>
    public string Format(string key, params object?[] args)
    {
        string template = Get(key);
        try
        {
            return string.Format(Culture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>The translated name, then the operator's own label, then its type.</summary>
    public string OperatorLabel(QueryOperator op) => Find("operators." + op.Type) ?? op.Label ?? op.Type;

    /// <summary>
    /// Gets the condition label.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>The condition label.</returns>
    public string ConditionLabel(string condition) => Find("conditions." + condition) ?? condition;

    private string? Find(string key)
    {
        foreach (var catalog in chain)
        {
            if (catalog.TryGetValue(key, out var value) && value.Length > 0)
            {
                return value;
            }
        }

        return null;
    }
}

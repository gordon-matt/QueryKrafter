namespace QueryKrafter.Localization;

/// <summary>
/// The strings for one language. The indexer returns English when this catalog has no value for
/// a key, and the key itself only when English has no value either.
/// </summary>
public sealed class QueryKrafterText
{
    public static QueryKrafterText English { get; } = QueryKrafterLocaleCatalog.Shared.Resolve("en");

    private readonly IReadOnlyList<Dictionary<string, string>> chain;

    internal QueryKrafterText(string language, CultureInfo culture, bool isRightToLeft, IReadOnlyList<Dictionary<string, string>> chain)
    {
        Language = language;
        Culture = culture;
        IsRightToLeft = isRightToLeft;
        this.chain = chain;
    }

    public string Language { get; }

    public CultureInfo Culture { get; }

    public bool IsRightToLeft { get; }

    public string this[string key] => Get(key);

    public string Get(string key) => Find(key) ?? key;

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

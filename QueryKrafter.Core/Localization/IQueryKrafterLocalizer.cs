namespace QueryKrafter.Localization;

/// <summary>A language the builder can show, named for a language menu.</summary>
public sealed record QueryKrafterLanguage(string Tag, string NativeName, bool IsRightToLeft);

/// <summary>Loads JSON catalogs and resolves a tag, including fallback to English.</summary>
public interface IQueryKrafterLocalizer
{
    /// <summary>
    /// Resolves the value.
    /// </summary>
    /// <param name="languageTag">The language tag.</param>
    /// <returns>The query krafter text.</returns>
    QueryKrafterText Resolve(string? languageTag);

    /// <summary>
    /// Gets the languages.
    /// </summary>
    IReadOnlyList<QueryKrafterLanguage> Languages { get; }
}

/// <summary>
/// Where extra language files live. Each file is named with an IETF tag, such as <c>fr-CA.json</c>,
/// and contains a flat object of string keys. Keys you leave out fall back to English.
/// </summary>
public sealed class QueryKrafterLocalizationOptions
{
    /// <summary>
    /// A folder of <c>{tag}.json</c> files. Files overlay the built-in catalog for that tag, and a
    /// tag with no built-in file becomes a new language. A path that does not exist is ignored.
    /// </summary>
    public string? Directory { get; set; }
}

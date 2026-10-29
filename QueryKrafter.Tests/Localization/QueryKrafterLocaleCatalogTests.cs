using QueryKrafter.Localization;

namespace QueryKrafter.Tests.Localization;

public class QueryKrafterLocaleCatalogTests
{
    private static readonly QueryKrafterLocaleCatalog Catalog = QueryKrafterLocaleCatalog.Shared;

    [Fact]
    public void Resolve_German_TranslatesLabels()
    {
        var text = Catalog.Resolve("de");

        Assert.Equal("Regel hinzufügen", text["group.addRule"]);
        Assert.Equal("ist gleich", text.OperatorLabel(QueryOperators.Defaults[0]));
    }

    [Fact]
    public void Resolve_RegionalTag_FallsBackToLanguage()
    {
        Assert.Equal("de", Catalog.Resolve("de-CH").Language);
    }

    [Fact]
    public void Resolve_UnknownLanguage_FallsBackToEnglish()
    {
        var text = Catalog.Resolve("xx");

        Assert.Equal("en", text.Language);
        Assert.Equal("Add rule", text["group.addRule"]);
    }

    [Fact]
    public void Resolve_MissingKey_ReturnsKey()
    {
        Assert.Equal("nope.nothing", Catalog.Resolve("fr")["nope.nothing"]);
    }

    [Fact]
    public void OperatorLabel_CustomOperator_UsesItsLabel()
    {
        Assert.Equal("sounds like", Catalog.Resolve("en").OperatorLabel(new() { Type = "sounds_like", Label = "sounds like" }));
    }

    [Fact]
    public void Languages_ListsEnglishFirst()
    {
        Assert.Equal("en", Catalog.Languages[0].Tag);
        Assert.Contains(Catalog.Languages, l => l.Tag == "zh-Hans");
    }

    [Fact]
    public void EmbeddedCatalogs_HaveEveryEnglishKey()
    {
        var assembly = typeof(QueryKrafterLocaleCatalog).Assembly;
        var english = Keys(assembly, "QueryKrafter.Locales.en.json");

        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("QueryKrafter.Locales.", StringComparison.Ordinal)))
        {
            var missing = english.Except(Keys(assembly, name)).ToList();
            Assert.True(missing.Count == 0, $"{name} is missing {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void EnglishCatalog_HasMessageForEveryErrorCode()
    {
        var english = Catalog.Resolve("en");
        var codes = typeof(QueryKrafter.Validation.QueryErrorCodes).GetFields().Select(f => (string)f.GetValue(null)!);

        foreach (string code in codes)
        {
            Assert.NotEqual("errors." + code, english["errors." + code]);
        }
    }

    private static HashSet<string> Keys(System.Reflection.Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        return [.. JsonNode.Parse(stream)!.AsObject().Select(p => p.Key)];
    }
}

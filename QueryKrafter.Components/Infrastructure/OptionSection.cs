namespace QueryKrafter.Components.Infrastructure;

/// <summary>Choices under one heading of a drop-down, in the order their headings first appear.</summary>
internal sealed record OptionSection<T>(string? Group, IReadOnlyList<T> Items)
{
    public static IReadOnlyList<OptionSection<T>> Build(
        IEnumerable<T> items,
        Func<T, string?> group,
        Func<T, string> label,
        bool sort,
        CultureInfo culture)
    {
        var sections = items
            .GroupBy(item => string.IsNullOrWhiteSpace(group(item)) ? null : group(item))
            .Select(g => new OptionSection<T>(
                g.Key,
                sort ? [.. g.OrderBy(label, StringComparer.Create(culture, ignoreCase: true))] : [.. g]))
            .ToList();

        return sections;
    }
}

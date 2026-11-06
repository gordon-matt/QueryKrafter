namespace QueryKrafter.Components;

/// <summary>Raised by <see cref="QueryBuilder.OnChanged"/> after every edit.</summary>
/// <param name="Rules">The builder's live tree. Clone it to keep a snapshot.</param>
public sealed record QueryChange(QueryGroup Rules, IReadOnlyList<QueryError> Errors)
{
    /// <summary>
    /// Gets a value indicating whether this instance is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>The rules in the jQuery QueryBuilder JSON format.</summary>
    public string ToJson(bool indented = false) => QueryJson.Serialize(Rules, indented);
}

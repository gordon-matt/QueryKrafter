namespace QueryKrafter.Sql;

/// <summary>
/// Specifies the SQL parameter style.
/// </summary>
public enum SqlParameterStyle
{
    /// <summary><c>@p1</c>, for ADO.NET providers and Dapper.</summary>
    Named,

    /// <summary><c>?</c>, for ODBC and OLE DB.</summary>
    Positional,

    /// <summary><c>$1</c>, for PostgreSQL's native protocol.</summary>
    Numbered,

    /// <summary>Values written as literals. For display; prefer parameters when running the query.</summary>
    Inline,
}

/// <summary>
/// How an operator is written. <c>{field}</c> is the filter's SQL, <c>{escape}</c> the dialect's
/// LIKE escape clause, and each <c>{value}</c> takes the next value in turn, or every value as a
/// comma-separated list for operators that take a list.
/// </summary>
/// <param name="ValueFormat">Wraps text values, e.g. <c>%{0}%</c> for <c>contains</c>.</param>
/// <param name="EscapeLike">Escapes LIKE wildcards in the value before <paramref name="ValueFormat"/> is applied.</param>
public sealed record SqlOperator(string Template, string ValueFormat = "{0}", bool EscapeLike = false);

/// <summary>
/// Provides SQL operators.
/// </summary>
public static class SqlOperators
{
    /// <summary>
    /// Gets the defaults.
    /// </summary>
    public static IReadOnlyDictionary<string, SqlOperator> Defaults { get; } = new Dictionary<string, SqlOperator>(StringComparer.Ordinal)
    {
        [QueryOperators.Equal] = new("{field} = {value}"),
        [QueryOperators.NotEqual] = new("{field} <> {value}"),
        [QueryOperators.In] = new("{field} IN ({value})"),
        [QueryOperators.NotIn] = new("{field} NOT IN ({value})"),
        [QueryOperators.Less] = new("{field} < {value}"),
        [QueryOperators.LessOrEqual] = new("{field} <= {value}"),
        [QueryOperators.Greater] = new("{field} > {value}"),
        [QueryOperators.GreaterOrEqual] = new("{field} >= {value}"),
        [QueryOperators.Between] = new("{field} BETWEEN {value} AND {value}"),
        [QueryOperators.NotBetween] = new("{field} NOT BETWEEN {value} AND {value}"),
        [QueryOperators.BeginsWith] = new("{field} LIKE {value}{escape}", "{0}%", EscapeLike: true),
        [QueryOperators.NotBeginsWith] = new("{field} NOT LIKE {value}{escape}", "{0}%", EscapeLike: true),
        [QueryOperators.Contains] = new("{field} LIKE {value}{escape}", "%{0}%", EscapeLike: true),
        [QueryOperators.NotContains] = new("{field} NOT LIKE {value}{escape}", "%{0}%", EscapeLike: true),
        [QueryOperators.EndsWith] = new("{field} LIKE {value}{escape}", "%{0}", EscapeLike: true),
        [QueryOperators.NotEndsWith] = new("{field} NOT LIKE {value}{escape}", "%{0}", EscapeLike: true),
        [QueryOperators.IsEmpty] = new("{field} = ''"),
        [QueryOperators.IsNotEmpty] = new("{field} <> ''"),
        [QueryOperators.IsNull] = new("{field} IS NULL"),
        [QueryOperators.IsNotNull] = new("{field} IS NOT NULL"),
    };
}

/// <summary>
/// Represents the SQL query parameter.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Value">The value.</param>
public sealed record SqlQueryParameter(string Name, object? Value);

/// <summary>A WHERE clause (without the keyword) and its parameters. Empty when the rules don't filter anything.</summary>
public sealed record SqlQuery(string Sql, IReadOnlyList<SqlQueryParameter> Parameters)
{
    /// <summary>
    /// Gets the empty.
    /// </summary>
    public static SqlQuery Empty { get; } = new(string.Empty, []);

    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    public bool IsEmpty => Sql.Length == 0;

    /// <summary>The parameters by name, e.g. for Dapper.</summary>
    public IReadOnlyDictionary<string, object?> ToDictionary() =>
        Parameters.ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
}

/// <summary>
/// Represents the SQL generator options.
/// </summary>
public sealed record SqlGeneratorOptions
{
    /// <summary>
    /// Gets the default.
    /// </summary>
    public static SqlGeneratorOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the dialect.
    /// </summary>
    public SqlDialect Dialect { get; init; } = SqlDialect.Ansi;

    /// <summary>
    /// Gets or sets the parameter style.
    /// </summary>
    public SqlParameterStyle ParameterStyle { get; init; } = SqlParameterStyle.Named;

    /// <summary>Parameters are named this followed by their position: <c>p1</c>, <c>p2</c>…</summary>
    public string ParameterPrefix { get; init; } = "p";

    /// <summary>Puts each rule on its own line, indented by group.</summary>
    public bool Indented { get; init; }

    /// <summary>Replaces or adds operator translations, by operator type.</summary>
    public IReadOnlyDictionary<string, SqlOperator>? Operators { get; init; }

    /// <summary>
    /// Writes a filter's SQL. Defaults to <see cref="QueryFilter.Field"/>, or the quoted
    /// <see cref="QueryFilter.Id"/>. The result is used verbatim, so never build it from user input.
    /// </summary>
    public Func<QueryFilter, SqlDialect, string>? FieldResolver { get; init; }
}

namespace QueryKrafter.Sql;

/// <summary>
/// How a database spells identifiers, literals, parameters and LIKE escapes. Use one of the
/// built-in dialects, or derive from this class for another database.
/// </summary>
public abstract class SqlDialect
{
    /// <summary>
    /// Gets the ansi.
    /// </summary>
    public static SqlDialect Ansi { get; } = new AnsiSqlDialect();

    /// <summary>
    /// Gets the SQL server.
    /// </summary>
    public static SqlDialect SqlServer { get; } = new SqlServerDialect();

    /// <summary>
    /// Gets the postgre SQL.
    /// </summary>
    public static SqlDialect PostgreSql { get; } = new PostgreSqlDialect();

    /// <summary>
    /// Gets the my SQL.
    /// </summary>
    public static SqlDialect MySql { get; } = new MySqlDialect();

    /// <summary>
    /// Gets the sqlite.
    /// </summary>
    public static SqlDialect Sqlite { get; } = new SqliteDialect();

    /// <summary>
    /// Gets the all.
    /// </summary>
    public static IReadOnlyList<SqlDialect> All { get; } = [Ansi, SqlServer, PostgreSql, MySql, Sqlite];

    /// <summary>
    /// Gets the name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the open quote.
    /// </summary>
    protected virtual string OpenQuote => "\"";

    /// <summary>
    /// Gets the close quote.
    /// </summary>
    protected virtual string CloseQuote => "\"";

    /// <summary>Put before a parameter name for <see cref="SqlParameterStyle.Named"/>.</summary>
    public virtual string NamedParameterMarker => "@";

    /// <summary>Appended to LIKE comparisons whose value was escaped with <see cref="EscapeLike"/>.</summary>
    public virtual string LikeEscapeClause => " ESCAPE '\\'";

    /// <summary>Finds a dialect by name or a common alias, e.g. <c>mssql</c> or <c>postgres</c>.</summary>
    public static SqlDialect? FromName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "ansi" or "sql" or "" => Ansi,
        "sqlserver" or "mssql" or "tsql" => SqlServer,
        "postgresql" or "postgres" or "pgsql" or "npgsql" => PostgreSql,
        "mysql" or "mariadb" => MySql,
        "sqlite" => Sqlite,
        _ => null,
    };

    /// <summary>Quotes each dot-separated part, so <c>orders.total</c> becomes <c>"orders"."total"</c>.</summary>
    public virtual string QuoteIdentifier(string name) =>
        string.Join('.', name.Split('.').Select(part => OpenQuote + part.Replace(CloseQuote, CloseQuote + CloseQuote, StringComparison.Ordinal) + CloseQuote));

    /// <summary>
    /// Formats the string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The string.</returns>
    public virtual string FormatString(string text) =>
        "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>
    /// Formats the boolean.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The boolean.</returns>
    public virtual string FormatBoolean(bool value) => value ? "TRUE" : "FALSE";

    /// <summary>Writes a value as a literal, for <see cref="SqlParameterStyle.Inline"/>.</summary>
    public virtual string FormatLiteral(object? value) => value switch
    {
        null => "NULL",
        string text => FormatString(text),
        bool flag => FormatBoolean(flag),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        DateOnly date => FormatString(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        TimeOnly time => FormatString(time.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture).TrimEnd('.')),
        TimeSpan span => FormatString(span.ToString("c", CultureInfo.InvariantCulture)),
        DateTime dateTime => FormatString(dateTime.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture).TrimEnd('.')),
        DateTimeOffset offset => FormatString(offset.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture)),
        IFormattable formattable when IsNumeric(value) => formattable.ToString(null, CultureInfo.InvariantCulture),
        IFormattable formattable => FormatString(formattable.ToString(null, CultureInfo.InvariantCulture)),
        _ => FormatString(value.ToString() ?? string.Empty),
    };

    /// <summary>Escapes LIKE wildcards in a value, so <c>50%</c> matches the text "50%" only.</summary>
    public virtual string EscapeLike(string text) => text
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>
    /// Gets the parameter.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="position">The position.</param>
    /// <param name="style">The style.</param>
    /// <returns>The parameter.</returns>
    public virtual string Parameter(string name, int position, SqlParameterStyle style) => style switch
    {
        SqlParameterStyle.Positional => "?",
        SqlParameterStyle.Numbered => "$" + position.ToString(CultureInfo.InvariantCulture),
        _ => NamedParameterMarker + name,
    };

    /// <inheritdoc/>
    public override string ToString() => Name;

    private static bool IsNumeric(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or decimal;
}

/// <summary>
/// Represents the ansi SQL dialect.
/// </summary>
public sealed class AnsiSqlDialect : SqlDialect
{
    /// <inheritdoc/>
    public override string Name => "ansi";
}

/// <summary>
/// Represents the SQL server dialect.
/// </summary>
public sealed class SqlServerDialect : SqlDialect
{
    /// <inheritdoc/>
    public override string Name => "sqlserver";

    /// <inheritdoc/>
    protected override string OpenQuote => "[";

    /// <inheritdoc/>
    protected override string CloseQuote => "]";

    /// <summary>Wildcards are escaped with brackets, which need no ESCAPE clause.</summary>
    public override string LikeEscapeClause => string.Empty;

    /// <inheritdoc/>
    public override string FormatString(string text) => "N" + base.FormatString(text);

    /// <inheritdoc/>
    public override string FormatBoolean(bool value) => value ? "1" : "0";

    /// <inheritdoc/>
    public override string EscapeLike(string text) => text
        .Replace("[", "[[]", StringComparison.Ordinal)
        .Replace("%", "[%]", StringComparison.Ordinal)
        .Replace("_", "[_]", StringComparison.Ordinal);
}

/// <summary>
/// Represents the postgre SQL dialect.
/// </summary>
public sealed class PostgreSqlDialect : SqlDialect
{
    /// <inheritdoc/>
    public override string Name => "postgresql";

    /// <summary>Backslash is already LIKE's escape character.</summary>
    public override string LikeEscapeClause => string.Empty;
}

/// <summary>
/// Represents the my SQL dialect.
/// </summary>
public sealed class MySqlDialect : SqlDialect
{
    /// <inheritdoc/>
    public override string Name => "mysql";

    /// <inheritdoc/>
    protected override string OpenQuote => "`";

    /// <inheritdoc/>
    protected override string CloseQuote => "`";

    /// <summary>Backslash is already LIKE's escape character.</summary>
    public override string LikeEscapeClause => string.Empty;

    /// <summary>Backslash escapes in MySQL string literals unless NO_BACKSLASH_ESCAPES is set, so it's doubled too.</summary>
    public override string FormatString(string text) =>
        "'" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal) + "'";
}

/// <summary>
/// Represents the sqlite dialect.
/// </summary>
public sealed class SqliteDialect : SqlDialect
{
    /// <inheritdoc/>
    public override string Name => "sqlite";

    /// <inheritdoc/>
    public override string FormatBoolean(bool value) => value ? "1" : "0";
}

namespace QueryKrafter.Sql;

/// <summary>
/// How a database spells identifiers, literals, parameters and LIKE escapes. Use one of the
/// built-in dialects, or derive from this class for another database.
/// </summary>
public abstract class SqlDialect
{
    public static SqlDialect Ansi { get; } = new AnsiSqlDialect();

    public static SqlDialect SqlServer { get; } = new SqlServerDialect();

    public static SqlDialect PostgreSql { get; } = new PostgreSqlDialect();

    public static SqlDialect MySql { get; } = new MySqlDialect();

    public static SqlDialect Sqlite { get; } = new SqliteDialect();

    public static IReadOnlyList<SqlDialect> All { get; } = [Ansi, SqlServer, PostgreSql, MySql, Sqlite];

    public abstract string Name { get; }

    protected virtual string OpenQuote => "\"";

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

    public virtual string FormatString(string text) =>
        "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";

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

    public virtual string Parameter(string name, int position, SqlParameterStyle style) => style switch
    {
        SqlParameterStyle.Positional => "?",
        SqlParameterStyle.Numbered => "$" + position.ToString(CultureInfo.InvariantCulture),
        _ => NamedParameterMarker + name,
    };

    public override string ToString() => Name;

    private static bool IsNumeric(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or decimal;
}

public sealed class AnsiSqlDialect : SqlDialect
{
    public override string Name => "ansi";
}

public sealed class SqlServerDialect : SqlDialect
{
    public override string Name => "sqlserver";

    protected override string OpenQuote => "[";

    protected override string CloseQuote => "]";

    /// <summary>Wildcards are escaped with brackets, which need no ESCAPE clause.</summary>
    public override string LikeEscapeClause => string.Empty;

    public override string FormatString(string text) => "N" + base.FormatString(text);

    public override string FormatBoolean(bool value) => value ? "1" : "0";

    public override string EscapeLike(string text) => text
        .Replace("[", "[[]", StringComparison.Ordinal)
        .Replace("%", "[%]", StringComparison.Ordinal)
        .Replace("_", "[_]", StringComparison.Ordinal);
}

public sealed class PostgreSqlDialect : SqlDialect
{
    public override string Name => "postgresql";

    /// <summary>Backslash is already LIKE's escape character.</summary>
    public override string LikeEscapeClause => string.Empty;
}

public sealed class MySqlDialect : SqlDialect
{
    public override string Name => "mysql";

    protected override string OpenQuote => "`";

    protected override string CloseQuote => "`";

    /// <summary>Backslash is already LIKE's escape character.</summary>
    public override string LikeEscapeClause => string.Empty;

    /// <summary>Backslash escapes in MySQL string literals unless NO_BACKSLASH_ESCAPES is set, so it's doubled too.</summary>
    public override string FormatString(string text) =>
        "'" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal) + "'";
}

public sealed class SqliteDialect : SqlDialect
{
    public override string Name => "sqlite";

    public override string FormatBoolean(bool value) => value ? "1" : "0";
}

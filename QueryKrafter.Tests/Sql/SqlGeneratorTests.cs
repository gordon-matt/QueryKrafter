using Microsoft.Data.Sqlite;
using QueryKrafter.Sql;
using static QueryKrafter.Tests.TestData;

namespace QueryKrafter.Tests.Sql;

public class SqlGeneratorTests
{
    [Fact]
    public void Generate_TwoRules_WritesNamedParameters()
    {
        var root = And(Rule("customer", QueryOperators.Equal, "Ann"), Rule("total", QueryOperators.Greater, 10));

        var sql = SqlGenerator.Generate(root, Config).Value;

        Assert.Equal("\"customer\" = @p1 AND \"total\" > @p2", sql.Sql);
        Assert.Equal("Ann", sql.Parameters[0].Value);
        Assert.Equal(10m, sql.Parameters[1].Value);
    }

    [Fact]
    public void Generate_NestedNotGroup_WrapsInNot()
    {
        var inner = Or(Rule("id", QueryOperators.Equal, 1), Rule("id", QueryOperators.Equal, 2));
        inner.Not = true;
        var root = And(Rule("shipped", QueryOperators.Equal, true), inner);

        var sql = SqlGenerator.Generate(root, Config, new() { ParameterStyle = SqlParameterStyle.Inline }).Value;

        Assert.Equal("\"shipped\" = TRUE AND NOT (\"id\" = 1 OR \"id\" = 2)", sql.Sql);
    }

    [Fact]
    public void Generate_FieldFromRuleJson_IsIgnored()
    {
        var rule = Rule("customer", QueryOperators.IsNull);
        rule.Field = "1=1; DROP TABLE orders; --";

        var sql = SqlGenerator.Generate(And(rule), Config).Value;

        Assert.Equal("\"customer\" IS NULL", sql.Sql);
    }

    [Fact]
    public void Generate_FilterField_IsUsedVerbatim()
    {
        var config = Config with { Filters = [new() { Id = "Orders-Total", Field = "\"Orders\".\"Total\"", Type = QueryValueType.Double }] };

        var sql = SqlGenerator.Generate(And(Rule("Orders-Total", QueryOperators.Less, 5)), config).Value;

        Assert.Equal("\"Orders\".\"Total\" < @p1", sql.Sql);
    }

    [Fact]
    public void Generate_UnknownFilter_ReturnsInvalid()
    {
        var result = SqlGenerator.Generate(And(Rule("password", QueryOperators.IsNotNull)), Config);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal("unknown_filter", Assert.Single(result.ValidationErrors).ErrorCode);
    }

    [Fact]
    public void Generate_EmptyRootWhenAllowed_ReturnsEmpty()
    {
        var result = SqlGenerator.Generate(new QueryGroup(), Config with { AllowEmpty = true });

        Assert.True(result.Value.IsEmpty);
    }

    [Theory]
    [InlineData("ansi", "\"customer\" LIKE @p1 ESCAPE '\\'", "%50\\%\\_off%")]
    [InlineData("sqlite", "\"customer\" LIKE @p1 ESCAPE '\\'", "%50\\%\\_off%")]
    [InlineData("postgresql", "\"customer\" LIKE @p1", "%50\\%\\_off%")]
    [InlineData("mysql", "`customer` LIKE @p1", "%50\\%\\_off%")]
    [InlineData("sqlserver", "[customer] LIKE @p1", "%50[%][_]off%")]
    public void Generate_Contains_EscapesWildcards(string dialect, string expectedSql, string expectedValue)
    {
        var sql = SqlGenerator.Generate(And(Rule("customer", QueryOperators.Contains, "50%_off")), Config, new() { Dialect = SqlDialect.FromName(dialect)! }).Value;

        Assert.Equal(expectedSql, sql.Sql);
        Assert.Equal(expectedValue, sql.Parameters[0].Value);
    }

    [Theory]
    [InlineData("ansi", "'O''Brien\\'")]
    [InlineData("sqlserver", "N'O''Brien\\'")]
    [InlineData("mysql", "'O''Brien\\\\'")]
    public void Generate_Inline_QuotesStrings(string dialect, string expectedLiteral)
    {
        var options = new SqlGeneratorOptions { Dialect = SqlDialect.FromName(dialect)!, ParameterStyle = SqlParameterStyle.Inline };

        var sql = SqlGenerator.Generate(And(Rule("customer", QueryOperators.Equal, "O'Brien\\")), Config, options).Value;

        Assert.EndsWith(" = " + expectedLiteral, sql.Sql, StringComparison.Ordinal);
        Assert.Empty(sql.Parameters);
    }

    [Fact]
    public void Generate_In_ListsEachValue()
    {
        var sql = SqlGenerator.Generate(And(Rule("status", QueryOperators.In, Array(1, 3))), Config).Value;

        Assert.Equal("\"status\" IN (@p1, @p2)", sql.Sql);
        Assert.Equal([1L, 3L], sql.Parameters.Select(p => p.Value));
    }

    [Fact]
    public void Generate_BetweenDates_UsesDateTimeParameters()
    {
        var sql = SqlGenerator.Generate(And(Rule("order_date", QueryOperators.Between, Array("2026-02-01", "2026-02-28"))), Config).Value;

        Assert.Equal("\"order_date\" BETWEEN @p1 AND @p2", sql.Sql);
        Assert.Equal(new DateTime(2026, 2, 1), sql.Parameters[0].Value);
    }

    [Theory]
    [InlineData(SqlParameterStyle.Positional, "\"id\" = ? OR \"id\" = ?")]
    [InlineData(SqlParameterStyle.Numbered, "\"id\" = $1 OR \"id\" = $2")]
    public void Generate_ParameterStyles(SqlParameterStyle style, string expected)
    {
        var sql = SqlGenerator.Generate(Or(Rule("id", QueryOperators.Equal, 1), Rule("id", QueryOperators.Equal, 2)), Config, new() { ParameterStyle = style }).Value;

        Assert.Equal(expected, sql.Sql);
    }

    [Fact]
    public void Generate_Indented_IndentsNestedGroups()
    {
        var root = And(Rule("id", QueryOperators.Equal, 1), Or(Rule("id", QueryOperators.Equal, 2), Rule("id", QueryOperators.Equal, 3)));

        var sql = SqlGenerator.Generate(root, Config, new() { Indented = true, ParameterStyle = SqlParameterStyle.Inline }).Value;

        Assert.Equal("\"id\" = 1\nAND (\n    \"id\" = 2\n    OR \"id\" = 3\n)", sql.Sql);
    }

    [Fact]
    public void Generate_HiddenRule_IsIncluded()
    {
        var hidden = Rule("shipped", QueryOperators.Equal, true);
        hidden.Hidden = true;

        var sql = SqlGenerator.Generate(And(Rule("id", QueryOperators.Greater, 0), hidden), Config).Value;

        Assert.Contains("\"shipped\"", sql.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_CustomOperator_UsesTemplate()
    {
        var config = Config with { Operators = [.. QueryOperators.Defaults, new() { Type = "sounds_like", AppliesTo = QueryValueKind.String }] };
        var options = new SqlGeneratorOptions { Operators = new Dictionary<string, SqlOperator> { ["sounds_like"] = new("SOUNDEX({field}) = SOUNDEX({value})") } };

        var sql = SqlGenerator.Generate(And(Rule("customer", "sounds_like", "Anne")), config, options).Value;

        Assert.Equal("SOUNDEX(\"customer\") = SOUNDEX(@p1)", sql.Sql);
    }

    [Fact]
    public void Generate_OperatorWithoutTranslation_ReturnsError()
    {
        var config = Config with { Operators = [.. QueryOperators.Defaults, new() { Type = "sounds_like", AppliesTo = QueryValueKind.String }] };

        var result = SqlGenerator.Generate(And(Rule("customer", "sounds_like", "Anne")), config);

        Assert.Equal(ResultStatus.Error, result.Status);
    }

    [Theory]
    [MemberData(nameof(SqliteCases))]
    public void Generate_RunsOnSqlite(string rulesJson, int[] expectedIds)
    {
        var sql = SqlGenerator.Generate(rulesJson, Config, new() { Dialect = SqlDialect.Sqlite }).Value;

        using var connection = OpenDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT id FROM orders WHERE {sql.Sql} ORDER BY id";
        foreach (var parameter in sql.Parameters)
        {
            command.Parameters.AddWithValue("@" + parameter.Name, parameter.Value is DateTime date ? date.ToString("yyyy-MM-dd") : parameter.Value);
        }

        var ids = new List<int>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ids.Add(reader.GetInt32(0));
        }

        Assert.Equal(expectedIds, ids);
    }

    public static TheoryData<string, int[]> SqliteCases => new()
    {
        { """{ "condition": "AND", "rules": [ { "id": "notes", "operator": "contains", "value": "50%" } ] }""", [2] },
        { """{ "condition": "AND", "rules": [ { "id": "notes", "operator": "begins_with", "value": "50" } ] }""", [2, 4] },
        { """{ "condition": "OR", "rules": [ { "id": "customer", "operator": "equal", "value": "O'Brien" }, { "id": "total", "operator": "less", "value": 10 } ] }""", [1, 3] },
        { """{ "condition": "AND", "rules": [ { "id": "status", "operator": "not_in", "value": [2] }, { "id": "total", "operator": "between", "value": [20, 60] } ] }""", [2, 4] },
        { """{ "condition": "AND", "not": true, "rules": [ { "id": "notes", "operator": "is_null" } ] }""", [2, 3, 4] },
        { """{ "condition": "AND", "rules": [ { "id": "order_date", "operator": "greater_or_equal", "value": "2026-02-10" } ] }""", [2, 3, 4] },
    };

    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE orders (id INTEGER, customer TEXT, total REAL, order_date TEXT, shipped INTEGER, status INTEGER, notes TEXT, city TEXT);
            INSERT INTO orders VALUES
                (1, 'Ann', 5, '2026-01-05', 1, 2, NULL, 'Oslo'),
                (2, 'Bob', 25, '2026-02-10', 0, 1, '50% off', NULL),
                (3, 'O''Brien', 100, '2026-02-10', 1, 2, 'rush', 'Dublin'),
                (4, 'annabel', 50.5, '2026-03-01', 0, 3, '500 units', 'Oslo');
            """;
        create.ExecuteNonQuery();
        return connection;
    }
}

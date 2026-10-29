using QueryKrafter.Linq;
using static QueryKrafter.Tests.TestData;

namespace QueryKrafter.Tests.Linq;

public class QueryExpressionBuilderTests
{
    private static int[] Ids(QueryGroup root, QueryBuilderConfig? config = null, QueryExpressionOptions? options = null)
    {
        var result = Orders.ApplyQuery(root, config ?? Config, options);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Concat(result.ValidationErrors.Select(e => e.ErrorMessage))));
        return [.. result.Value.Select(o => o.Id)];
    }

    [Fact]
    public void Build_EqualOrGreater_FiltersList()
    {
        int[] ids = Ids(Or(Rule("customer", QueryOperators.Equal, "Ann"), Rule("total", QueryOperators.Greater, 50)));

        Assert.Equal([1, 3, 4], ids);
    }

    [Fact]
    public void Build_NestedPath_SkipsNullParents()
    {
        int[] ids = Ids(And(Rule("city", QueryOperators.Equal, "Oslo")));

        Assert.Equal([1, 4], ids);
    }

    [Fact]
    public void Build_EnumIn_MatchesByNumber()
    {
        int[] ids = Ids(And(Rule("status", QueryOperators.In, Array(1, 3))));

        Assert.Equal([2, 4], ids);
    }

    [Fact]
    public void Build_DateOnDateTimeProperty_IgnoresTimeOfDay()
    {
        int[] ids = Ids(And(Rule("order_date", QueryOperators.Equal, "2026-02-10")));

        Assert.Equal([2, 3], ids);
    }

    [Fact]
    public void Build_Between_IsInclusive()
    {
        int[] ids = Ids(And(Rule("total", QueryOperators.Between, Array(25, 100))));

        Assert.Equal([2, 3, 4], ids);
    }

    [Fact]
    public void Build_CaseInsensitive_MatchesAnyCase()
    {
        int[] sensitive = Ids(And(Rule("customer", QueryOperators.BeginsWith, "ann")));
        int[] insensitive = Ids(And(Rule("customer", QueryOperators.BeginsWith, "ann")), options: new() { CaseInsensitive = true });

        Assert.Equal([4], sensitive);
        Assert.Equal([1, 4], insensitive);
    }

    [Fact]
    public void Build_ContainsOnNullableText_SkipsNulls()
    {
        int[] ids = Ids(And(Rule("notes", QueryOperators.Contains, "u")));

        Assert.Equal([3, 4], ids);
    }

    [Fact]
    public void Build_NotGroup_Negates()
    {
        var root = And(Rule("shipped", QueryOperators.Equal, true));
        root.Not = true;

        Assert.Equal([2, 4], Ids(root));
    }

    [Fact]
    public void Build_IsNull_OnNonNullableProperty_MatchesNothing()
    {
        Assert.Empty(Ids(And(Rule("total", QueryOperators.IsNull))));
    }

    [Fact]
    public void Build_EmptyRootWhenAllowed_MatchesEverything()
    {
        Assert.Equal(4, Ids(new QueryGroup(), Config with { AllowEmpty = true }).Length);
    }

    [Fact]
    public void Build_UnknownProperty_ReturnsError()
    {
        var config = Config with { Filters = [new() { Id = "missing", Property = "Nope" }] };

        var result = QueryExpressionBuilder.Build<Order>(And(Rule("missing", QueryOperators.IsNull)), config);

        Assert.Equal(ResultStatus.Error, result.Status);
    }

    [Fact]
    public void Build_InvalidValue_ReturnsInvalid()
    {
        var result = QueryExpressionBuilder.Build<Order>(And(Rule("total", QueryOperators.Equal, "lots")), Config);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public void Build_CustomOperator_UsesDelegate()
    {
        var config = Config with { Operators = [.. QueryOperators.Defaults, new() { Type = "longer_than", AppliesTo = QueryValueKind.String }] };
        var options = new QueryExpressionOptions
        {
            Operators = new Dictionary<string, QueryOperatorExpression>
            {
                ["longer_than"] = (property, values) => System.Linq.Expressions.Expression.GreaterThan(
                    System.Linq.Expressions.Expression.Property(property, nameof(string.Length)),
                    System.Linq.Expressions.Expression.Call(typeof(int), nameof(int.Parse), null, values[0])),
            },
        };

        int[] ids = Ids(And(Rule("customer", "longer_than", "3")), config, options);

        Assert.Equal([3, 4], ids);
    }

    [Fact]
    public void ApplyQuery_Queryable_KeepsExpressionTree()
    {
        var result = Orders.AsQueryable().ApplyQuery(And(Rule("id", QueryOperators.Less, 3)), Config);

        Assert.Contains("Where", result.Value.Expression.ToString(), StringComparison.Ordinal);
        Assert.Equal([1, 2], result.Value.Select(o => o.Id));
    }
}

using QueryKrafter.Linq;
using static QueryKrafter.Tests.TestData;

namespace QueryKrafter.Tests;

public class QueryInverterTests
{
    [Fact]
    public void Invert_SwapsConditionsAndOperators()
    {
        var root = And(Rule("total", QueryOperators.Less, 10), Or(Rule("customer", QueryOperators.Contains, "a"), Rule("notes", QueryOperators.IsNull)));

        var inverted = QueryInverter.Invert(root, Config).Value;

        Assert.Equal(Constants.Conditions.Or, inverted.Condition);
        Assert.Equal(QueryOperators.GreaterOrEqual, ((QueryRule)inverted.Rules[0]).Operator);
        var nested = (QueryGroup)inverted.Rules[1];
        Assert.Equal(Constants.Conditions.And, nested.Condition);
        Assert.Equal([QueryOperators.NotContains, QueryOperators.IsNotNull], nested.Rules.Cast<QueryRule>().Select(r => r.Operator));
    }

    [Fact]
    public void Invert_NotGroup_RemovesNot()
    {
        var root = And(Rule("id", QueryOperators.Equal, 1));
        root.Not = true;

        var inverted = QueryInverter.Invert(root, Config).Value;

        Assert.False(inverted.Not);
        Assert.Equal(QueryOperators.Equal, ((QueryRule)inverted.Rules[0]).Operator);
    }

    [Fact]
    public void Invert_LeavesOriginalAsItIs()
    {
        var root = And(Rule("id", QueryOperators.Equal, 1));

        _ = QueryInverter.Invert(root, Config);

        Assert.Equal(Constants.Conditions.And, root.Condition);
        Assert.Equal(QueryOperators.Equal, ((QueryRule)root.Rules[0]).Operator);
    }

    [Fact]
    public void Invert_OperatorWithoutOpposite_WrapsInNotGroup()
    {
        var config = Config with { Operators = [.. QueryOperators.Defaults, new() { Type = "odd", AppliesTo = QueryValueKind.Number }] };

        var inverted = QueryInverter.Invert(And(Rule("id", "odd", 1)), config).Value;

        var wrapper = Assert.IsType<QueryGroup>(Assert.Single(inverted.Rules));
        Assert.True(wrapper.Not);
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public void Invert_MatchesExactlyTheOtherOrders(string json)
    {
        var root = QueryJson.Parse(json);
        var inverted = QueryInverter.Invert(root, Config).Value;

        var matched = Orders.ApplyQuery(root, Config).Value.Select(o => o.Id).ToHashSet();
        var others = Orders.ApplyQuery(inverted, Config).Value.Select(o => o.Id).ToHashSet();

        Assert.Empty(matched.Intersect(others));
        Assert.Equal(Orders.Count, matched.Count + others.Count);
    }

    public static TheoryData<string> Queries =>
    [
        """{ "condition": "AND", "rules": [ { "id": "total", "operator": "between", "value": [10, 60] }, { "id": "shipped", "operator": "equal", "value": false } ] }""",
        """{ "condition": "OR", "rules": [ { "id": "status", "operator": "in", "value": [3] }, { "condition": "AND", "rules": [ { "id": "customer", "operator": "begins_with", "value": "A" }, { "id": "id", "operator": "less_or_equal", "value": 2 } ] } ] }""",
    ];
}

namespace QueryKrafter.Tests.Serialization;

public class QueryJsonTests
{
    /// <summary>Saved by Queryz with the design-mode fork of jQuery QueryBuilder.</summary>
    private const string QueryzRules = """
        {
          "condition": "AND",
          "rules": [
            { "id": "Orders-Total", "field": "\"Orders\".\"Total\"", "type": "double", "input": "number", "operator": "greater", "value": 10 },
            {
              "condition": "OR",
              "rules": [
                { "id": "Orders-Status", "field": "\"Orders\".\"Status\"", "type": "integer", "input": "checkbox", "operator": "in", "value": [1, 2], "data": { "show": false } },
                { "id": "Orders-Customer", "field": "\"Orders\".\"Customer\"", "type": "string", "input": "text", "operator": "begins_with", "value": "A" }
              ],
              "not": true
            }
          ],
          "valid": true
        }
        """;

    [Fact]
    public void Parse_ForkHiddenRule_SetsHiddenAndKeepsOtherData()
    {
        var group = QueryJson.Parse("""{ "condition": "AND", "rules": [ { "id": "a", "operator": "equal", "value": 1, "data": { "show": false, "mine": 2 } } ] }""");

        var rule = Assert.IsType<QueryRule>(Assert.Single(group.Rules));
        Assert.True(rule.Hidden);
        Assert.NotNull(rule.Data);
        Assert.False(rule.Data.ContainsKey("show"));
        Assert.Equal(2, rule.Data["mine"]!.GetValue<int>());
    }

    [Fact]
    public void ToJson_HiddenRule_WritesDataShowFalse()
    {
        var rule = new QueryRule { Id = "a", Operator = "equal", Value = 1, Hidden = true };

        var json = QueryJson.ToJson(rule);

        Assert.False(json["data"]!["show"]!.GetValue<bool>());
        Assert.Equal("a", json["field"]!.GetValue<string>());
    }

    [Fact]
    public void Parse_VisibleRule_WritesNoData()
    {
        var group = QueryJson.Parse("""{ "condition": "AND", "rules": [ { "id": "a", "operator": "equal", "value": 1, "data": { "show": true } } ] }""");

        var json = QueryJson.ToJson((QueryRule)group.Rules[0]);

        Assert.False(json.ContainsKey("data"));
    }

    [Fact]
    public void Parse_QueryzRules_RoundTripsUnchanged()
    {
        var group = QueryJson.Parse(QueryzRules);
        var reparsed = QueryJson.Parse(QueryJson.Serialize(group));

        var nested = Assert.IsType<QueryGroup>(reparsed.Rules[1]);
        Assert.True(nested.Not);
        Assert.Equal("OR", nested.Condition);
        var status = Assert.IsType<QueryRule>(nested.Rules[0]);
        Assert.True(status.Hidden);
        Assert.Equal("\"Orders\".\"Status\"", status.Field);
        Assert.Equal(QueryInputType.Checkbox, status.Input);
        Assert.Equal("[1,2]", status.Value!.ToJsonString());
    }

    [Fact]
    public void Parse_ArrayRoot_ReadsAndGroup()
    {
        var group = QueryJson.Parse("""[ { "id": "a", "operator": "is_null" }, { "id": "b", "operator": "is_null" } ]""");

        Assert.Equal(Constants.Conditions.And, group.Condition);
        Assert.Equal(2, group.Rules.Count);
    }

    [Fact]
    public void Parse_LowercaseCondition_IsUppercased()
    {
        var group = QueryJson.Parse("""{ "condition": "or", "rules": [] }""");

        Assert.Equal("OR", group.Condition);
    }

    [Fact]
    public void Parse_ReadonlyShortcut_SetsEveryFlag()
    {
        var group = QueryJson.Parse("""{ "condition": "AND", "readonly": true, "rules": [ { "id": "a", "operator": "is_null", "readonly": true } ] }""");

        Assert.Equal(GroupFlags.ReadOnly, group.Flags);
        Assert.Equal(RuleFlags.ReadOnly, ((QueryRule)group.Rules[0]).Flags);
    }

    [Fact]
    public void Parse_SnakeCaseFlags_RoundTrip()
    {
        var group = QueryJson.Parse("""{ "condition": "AND", "rules": [ { "id": "a", "operator": "is_null", "flags": { "operator_readonly": true, "no_delete": true } } ] }""");

        var json = QueryJson.ToJson((QueryRule)group.Rules[0]);

        Assert.True(json["flags"]!["operator_readonly"]!.GetValue<bool>());
        Assert.True(json["flags"]!["no_delete"]!.GetValue<bool>());
        Assert.False(((JsonObject)json["flags"]!).ContainsKey("filter_readonly"));
    }

    [Fact]
    public void Parse_EmptyRuleFlag_ClearsFilter()
    {
        var group = QueryJson.Parse("""{ "condition": "AND", "rules": [ { "id": "a", "field": "a", "empty": true } ] }""");

        Assert.Null(((QueryRule)group.Rules[0]).Id);
    }

    [Fact]
    public void Parse_NumericId_ReadsAsText()
    {
        var group = QueryJson.Parse("""{ "condition": "AND", "rules": [ { "id": 7, "operator": "is_null" } ] }""");

        Assert.Equal("7", ((QueryRule)group.Rules[0]).Id);
    }

    [Fact]
    public void TryParse_Blank_ReturnsEmptyGroup()
    {
        var result = QueryJson.TryParse("  ");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsEmpty);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("42")]
    [InlineData("""{ "condition": "AND" }""")]
    public void TryParse_Malformed_ReturnsInvalid(string json)
    {
        var result = QueryJson.TryParse(json);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public void JsonSerializer_QueryGroup_UsesRulesFormat()
    {
        var group = TestData.And(TestData.Rule("customer", QueryOperators.Equal, "Ann"));

        string json = System.Text.Json.JsonSerializer.Serialize(group);
        var back = System.Text.Json.JsonSerializer.Deserialize<QueryGroup>(json)!;

        Assert.Contains("\"condition\":\"AND\"", json, StringComparison.Ordinal);
        Assert.Equal("Ann", ((QueryRule)back.Rules[0]).Value!.GetValue<string>());
    }

    [Fact]
    public void SerializeConfig_RoundTripsFiltersAndOptions()
    {
        var config = TestData.Config with { MaxGroupDepth = 2, AllowSorting = true };

        var back = QueryJson.ParseConfig(QueryJson.SerializeConfig(config, indented: true));

        Assert.Equal(config.Filters.Count, back.Filters.Count);
        Assert.Equal(2, back.MaxGroupDepth);
        Assert.True(back.AllowSorting);
        var status = back.FindFilter("status")!;
        Assert.Equal(QueryValueType.Integer, status.Type);
        Assert.Equal("Cancelled", status.Values[2].Label);
        Assert.Equal(QueryOperators.PickList, status.Operators);
    }

    [Fact]
    public void ParseConfig_NumericOptionValues_ReadAsText()
    {
        var config = QueryJson.ParseConfig("""{ "filters": [ { "id": "s", "type": "integer", "values": [ { "value": 1, "label": "One" } ] } ] }""");

        Assert.Equal("1", config.Filters[0].Values[0].Value);
        Assert.Equal(QueryInputType.Select, config.Filters[0].EffectiveInput);
    }
}

using QueryKrafter.Localization;
using QueryKrafter.Validation;
using static QueryKrafter.Tests.TestData;

namespace QueryKrafter.Tests.Validation;

public class QueryValidatorTests
{
    private static string[] Codes(QueryGroup root, QueryBuilderConfig? config = null, bool skipEmpty = false) =>
        [.. QueryValidator.Validate(root, config ?? Config, skipEmpty).Select(e => e.Code)];

    [Fact]
    public void Validate_EmptyRoot_ReportsEmptyGroup()
    {
        Assert.Equal([QueryErrorCodes.EmptyGroup], Codes(new QueryGroup()));
    }

    [Fact]
    public void Validate_EmptyRootWhenAllowed_IsValid()
    {
        Assert.Empty(Codes(new QueryGroup(), Config with { AllowEmpty = true }));
    }

    [Fact]
    public void Validate_RuleWithoutFilter_ReportsNoFilter()
    {
        Assert.Equal([QueryErrorCodes.NoFilter], Codes(And(new QueryRule())));
    }

    [Fact]
    public void Validate_SkipEmpty_IgnoresUnfinishedRules()
    {
        Assert.Empty(Codes(And(Rule("id", QueryOperators.Equal, 1), new QueryRule()), skipEmpty: true));
    }

    [Theory]
    [InlineData("total", QueryOperators.Equal, "abc", QueryErrorCodes.NumberNaN)]
    [InlineData("id", QueryOperators.Equal, "1.5", QueryErrorCodes.NumberNotInteger)]
    [InlineData("customer", QueryOperators.Equal, "", QueryErrorCodes.StringEmpty)]
    [InlineData("order_date", QueryOperators.Equal, "next week", QueryErrorCodes.DateTimeInvalid)]
    [InlineData("shipped", QueryOperators.Equal, "maybe", QueryErrorCodes.BooleanNotValid)]
    [InlineData("total", QueryOperators.BeginsWith, "1", QueryErrorCodes.InvalidOperator)]
    public void Validate_BadValue_ReportsCode(string filter, string op, string value, string expected)
    {
        Assert.Equal([expected], Codes(And(Rule(filter, op, value))));
    }

    [Fact]
    public void Validate_EmptySelect_ReportsSelectEmpty()
    {
        Assert.Equal([QueryErrorCodes.SelectEmpty], Codes(And(Rule("status", QueryOperators.Equal))));
    }

    [Fact]
    public void Validate_ReversedBetween_ReportsBetweenInvalid()
    {
        Assert.Equal([QueryErrorCodes.NumberBetweenInvalid], Codes(And(Rule("total", QueryOperators.Between, Array(10, 5)))));
    }

    [Fact]
    public void Validate_ListForSingleValueOperator_ReportsNotMultiple()
    {
        Assert.Equal([QueryErrorCodes.OperatorNotMultiple], Codes(And(Rule("id", QueryOperators.Equal, Array(1, 2)))));
    }

    [Fact]
    public void Validate_FilterValidation_ChecksLengthAndBounds()
    {
        var config = Config with
        {
            Filters =
            [
                new() { Id = "code", Validation = new() { Max = "3" } },
                new() { Id = "qty", Type = QueryValueType.Integer, Validation = new() { Min = "1", Step = 5 } },
            ],
        };

        Assert.Equal([QueryErrorCodes.StringTooLong], Codes(And(Rule("code", QueryOperators.Equal, "ABCD")), config));
        Assert.Equal([QueryErrorCodes.NumberTooSmall], Codes(And(Rule("qty", QueryOperators.Equal, 0)), config));
        Assert.Equal([QueryErrorCodes.NumberWrongStep], Codes(And(Rule("qty", QueryOperators.Equal, 7)), config));
        Assert.Empty(Codes(And(Rule("qty", QueryOperators.Equal, 10)), config));
    }

    [Fact]
    public void Validate_UniqueFilterUsedTwice_ReportsNotUnique()
    {
        var config = Config with { Filters = [new() { Id = "a", Unique = UniqueScope.Query }] };

        var codes = Codes(And(Rule("a", QueryOperators.IsNull), Or(Rule("a", QueryOperators.IsNotNull))), config);

        Assert.Equal([QueryErrorCodes.FilterNotUnique], codes);
    }

    [Fact]
    public void Validate_GroupBeyondMaxDepth_ReportsTooManyGroups()
    {
        var root = And(Rule("id", QueryOperators.Equal, 1), Or(Rule("id", QueryOperators.Equal, 2)));

        Assert.Equal([QueryErrorCodes.TooManyGroups], Codes(root, Config with { MaxGroupDepth = 0 }));
        Assert.Empty(Codes(root, Config with { MaxGroupDepth = 1 }));
    }

    [Fact]
    public void Validate_UnknownCondition_ReportsInvalidCondition()
    {
        var root = And(Rule("id", QueryOperators.Equal, 1));
        root.Condition = "XOR";

        Assert.Equal([QueryErrorCodes.InvalidCondition], Codes(root));
    }

    [Fact]
    public void Validate_CustomValidator_RunsAfterBuiltInChecks()
    {
        var config = Config with
        {
            RuleValidator = (rule, filter) => filter.Id == "customer" && rule.Value?.GetValue<string>() == "Mallory"
                ? new QueryRuleError("not_allowed", "Mallory")
                : null,
        };

        Assert.Equal(["not_allowed"], Codes(And(Rule("customer", QueryOperators.Equal, "Mallory")), config));
    }

    [Fact]
    public void Validate_ErrorPath_LocatesNestedRule()
    {
        var errors = QueryValidator.Validate(And(Rule("id", QueryOperators.Equal, 1), Or(Rule("id", QueryOperators.Equal, 2), Rule("total", QueryOperators.Equal, "x"))), Config);

        Assert.Equal("1.1", Assert.Single(errors).Path);
    }

    [Fact]
    public void ToResult_German_TranslatesMessages()
    {
        var text = QueryKrafterLocaleCatalog.Shared.Resolve("de");
        var errors = QueryValidator.Validate(And(Rule("total", QueryOperators.Equal, "x")), Config, text: text);

        var result = QueryValidator.ToResult(errors, Config, text);

        Assert.Equal("Zahl eingeben", Assert.Single(result.ValidationErrors).ErrorMessage);
    }

    [Fact]
    public void ToResult_FilterMessage_ReplacesBuiltIn()
    {
        var config = Config with
        {
            Filters = [new() { Id = "sku", Validation = new() { Format = "^[A-Z]{3}$", Messages = new Dictionary<string, string> { [QueryErrorCodes.StringInvalidFormat] = "Three capital letters" } } }],
        };

        var result = QueryValidator.ToResult(QueryValidator.Validate(And(Rule("sku", QueryOperators.Equal, "ab")), config), config);

        Assert.Equal("Three capital letters", Assert.Single(result.ValidationErrors).ErrorMessage);
    }
}

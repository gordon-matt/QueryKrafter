using System.ComponentModel.DataAnnotations;

namespace QueryKrafter.Tests;

public class QueryFiltersTests
{
    private sealed class Product
    {
        [Display(Name = "Product name", Order = 1)]
        public string Name { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        public int? Stock { get; set; }

        public OrderStatus Status { get; set; }

        public DateOnly Released { get; set; }

        [Display(AutoGenerateFilter = false)]
        public string Secret { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = [];
    }

    private static readonly IReadOnlyList<QueryFilter> Filters = QueryFilters.FromType<Product>();

    [Fact]
    public void FromType_SkipsExcludedAndComplexProperties()
    {
        Assert.Equal(["Name", "UnitPrice", "Stock", "Status", "Released"], Filters.Select(f => f.Id));
    }

    [Fact]
    public void FromType_UsesDisplayNameOrHumanizedName()
    {
        Assert.Equal("Product name", Filters[0].Label);
        Assert.Equal("Unit price", Filters[1].Label);
    }

    [Fact]
    public void FromType_MapsTypes()
    {
        Assert.Equal(
            [QueryValueType.String, QueryValueType.Double, QueryValueType.Integer, QueryValueType.Integer, QueryValueType.Date],
            Filters.Select(f => f.Type));
    }

    [Fact]
    public void FromType_NonNullableValue_OmitsNullOperators()
    {
        Assert.DoesNotContain(QueryOperators.IsNull, Filters[1].Operators!);
        Assert.Null(Filters[2].Operators);
    }

    [Fact]
    public void FromType_Enum_BecomesSelect()
    {
        var status = Filters[3];

        Assert.Equal(QueryInputType.Select, status.EffectiveInput);
        Assert.Equal(["1", "2", "3"], status.Values.Select(v => v.Value));
        Assert.DoesNotContain(QueryOperators.IsNull, status.Operators!);
    }

    [Theory]
    [InlineData("OrderDate", "Order date")]
    [InlineData("OrderID", "Order ID")]
    [InlineData("HTMLBody", "HTML body")]
    [InlineData("Name", "Name")]
    public void Humanize_SplitsPascalCase(string name, string expected)
    {
        Assert.Equal(expected, QueryFilters.Humanize(name));
    }
}

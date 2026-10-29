namespace QueryKrafter.Tests;

public enum OrderStatus
{
    Pending = 1,
    Shipped = 2,
    Cancelled = 3,
}

public sealed class Address
{
    public string City { get; set; } = string.Empty;
}

public sealed class Order
{
    public int Id { get; set; }

    public string Customer { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public DateTime OrderDate { get; set; }

    public bool Shipped { get; set; }

    public OrderStatus Status { get; set; }

    public string? Notes { get; set; }

    public Address? Address { get; set; }
}

/// <summary>Filter ids match the SQLite columns in <see cref="SqlGeneratorTests"/>; properties match <see cref="Order"/>.</summary>
internal static class TestData
{
    public static QueryBuilderConfig Config { get; } = new()
    {
        AllowNot = true,
        Filters =
        [
            new() { Id = "id", Label = "Id", Type = QueryValueType.Integer, Property = nameof(Order.Id) },
            new() { Id = "customer", Label = "Customer", Property = nameof(Order.Customer) },
            new() { Id = "total", Label = "Total", Type = QueryValueType.Double, Property = nameof(Order.Total) },
            new() { Id = "order_date", Label = "Order date", Type = QueryValueType.Date, Property = nameof(Order.OrderDate) },
            new() { Id = "shipped", Label = "Shipped", Type = QueryValueType.Boolean, Property = nameof(Order.Shipped) },
            new()
            {
                Id = "status",
                Label = "Status",
                Type = QueryValueType.Integer,
                Property = nameof(Order.Status),
                Operators = QueryOperators.PickList,
                Values = [new("1", "Pending"), new("2", "Shipped"), new("3", "Cancelled")],
            },
            new() { Id = "notes", Label = "Notes", Property = nameof(Order.Notes) },
            new() { Id = "city", Label = "City", Property = "Address.City" },
        ],
    };

    public static IReadOnlyList<Order> Orders { get; } =
    [
        new() { Id = 1, Customer = "Ann", Total = 5m, OrderDate = new(2026, 1, 5, 9, 30, 0), Shipped = true, Status = OrderStatus.Shipped, Address = new() { City = "Oslo" } },
        new() { Id = 2, Customer = "Bob", Total = 25m, OrderDate = new(2026, 2, 10, 14, 0, 0), Shipped = false, Status = OrderStatus.Pending, Notes = "50% off" },
        new() { Id = 3, Customer = "O'Brien", Total = 100m, OrderDate = new(2026, 2, 10, 23, 59, 0), Shipped = true, Status = OrderStatus.Shipped, Notes = "rush", Address = new() { City = "Dublin" } },
        new() { Id = 4, Customer = "annabel", Total = 50.5m, OrderDate = new(2026, 3, 1), Shipped = false, Status = OrderStatus.Cancelled, Notes = "500 units", Address = new() { City = "Oslo" } },
    ];

    public static QueryRule Rule(string id, string op, JsonNode? value = null) =>
        new() { Id = id, Field = id, Operator = op, Value = value };

    public static QueryGroup And(params QueryNode[] rules) => new() { Condition = Constants.Conditions.And, Rules = [.. rules] };

    public static QueryGroup Or(params QueryNode[] rules) => new() { Condition = Constants.Conditions.Or, Rules = [.. rules] };

    public static JsonArray Array(params JsonNode?[] values) => new(values);
}

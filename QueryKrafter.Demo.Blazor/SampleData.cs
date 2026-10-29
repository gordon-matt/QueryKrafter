namespace QueryKrafter.Demo;

public enum OrderStatus
{
    Pending = 1,
    Shipped = 2,
    Cancelled = 3,
}

public sealed class Order
{
    public int Id { get; init; }

    public string Customer { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public DateTime OrderDate { get; init; }

    public bool Shipped { get; init; }

    public OrderStatus Status { get; init; }

    public string? Notes { get; init; }
}

/// <summary>The orders both demos filter, and the configuration that describes them.</summary>
public static class SampleData
{
    public static QueryBuilderConfig Config { get; } = new()
    {
        AllowNot = true,
        AllowInvert = true,
        AllowSorting = true,
        Filters =
        [
            new() { Id = "customer", Label = "Customer", Field = "customer", Property = nameof(Order.Customer), Group = "Order" },
            new() { Id = "total", Label = "Total", Field = "total", Type = QueryValueType.Double, Property = nameof(Order.Total), Group = "Order", Validation = new() { Min = "0" } },
            new() { Id = "order_date", Label = "Order date", Field = "order_date", Type = QueryValueType.Date, Property = nameof(Order.OrderDate), Group = "Order" },
            new() { Id = "shipped", Label = "Shipped", Field = "shipped", Type = QueryValueType.Boolean, Property = nameof(Order.Shipped), Group = "Order" },
            new()
            {
                Id = "status",
                Label = "Status",
                Field = "status",
                Type = QueryValueType.Integer,
                Input = QueryInputType.Checkbox,
                Property = nameof(Order.Status),
                Group = "Order",
                Operators = [QueryOperators.In, QueryOperators.NotIn],
                Values = [new("1", "Pending"), new("2", "Shipped"), new("3", "Cancelled")],
            },
            new() { Id = "notes", Label = "Notes", Field = "notes", Property = nameof(Order.Notes), Group = "Order" },
        ],
    };

    public static IReadOnlyList<Order> Orders { get; } =
    [
        new() { Id = 1, Customer = "Ann", Total = 5m, OrderDate = new(2026, 1, 5), Shipped = true, Status = OrderStatus.Shipped },
        new() { Id = 2, Customer = "Bob", Total = 25m, OrderDate = new(2026, 2, 10), Shipped = false, Status = OrderStatus.Pending, Notes = "50% off" },
        new() { Id = 3, Customer = "O'Brien", Total = 100m, OrderDate = new(2026, 2, 10), Shipped = true, Status = OrderStatus.Shipped, Notes = "rush" },
        new() { Id = 4, Customer = "Annabel", Total = 50.5m, OrderDate = new(2026, 3, 1), Shipped = false, Status = OrderStatus.Cancelled, Notes = "500 units" },
    ];

    public static string ConfigJson => QueryJson.SerializeConfig(Config);

    public static string StarterRules => """
        {
          "condition": "AND",
          "rules": [
            { "id": "total", "operator": "greater", "value": 10 },
            { "id": "status", "operator": "in", "value": [1, 2], "data": { "show": false } }
          ]
        }
        """;
}

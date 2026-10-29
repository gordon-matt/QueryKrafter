using QueryKrafter.Serialization;

namespace QueryKrafter.Models;

/// <summary>
/// A rule or a group of rules. The tree is mutable so the builder can edit it in place; its JSON
/// is the jQuery QueryBuilder rules format (see <see cref="QueryJson"/>).
/// </summary>
[JsonConverter(typeof(QueryNodeJsonConverter))]
public abstract class QueryNode
{
    /// <summary>Your own data, kept with the node through JSON round trips.</summary>
    public JsonObject? Data { get; set; }

    public abstract QueryNode Clone();
}

[JsonConverter(typeof(QueryGroupJsonConverter))]
public sealed class QueryGroup : QueryNode
{
    public string Condition { get; set; } = Constants.Conditions.And;

    /// <summary>Negates the whole group.</summary>
    public bool Not { get; set; }

    public List<QueryNode> Rules { get; set; } = [];

    public GroupFlags Flags { get; set; } = GroupFlags.None;

    [JsonIgnore]
    public bool IsEmpty => Rules.Count == 0;

    /// <summary>Every rule in this group and the groups inside it.</summary>
    public IEnumerable<QueryRule> AllRules() => Rules.SelectMany(node => node switch
    {
        QueryRule rule => [rule],
        QueryGroup group => group.AllRules(),
        _ => [],
    });

    /// <summary>Finds the group that directly contains <paramref name="node"/>.</summary>
    public QueryGroup? ParentOf(QueryNode node)
    {
        foreach (var child in Rules)
        {
            if (ReferenceEquals(child, node))
            {
                return this;
            }

            if (child is QueryGroup group && group.ParentOf(node) is { } parent)
            {
                return parent;
            }
        }

        return null;
    }

    public bool Contains(QueryNode node) => ParentOf(node) is not null;

    public override QueryGroup Clone() => new()
    {
        Condition = Condition,
        Not = Not,
        Flags = Flags,
        Data = Data?.DeepClone().AsObject(),
        Rules = [.. Rules.Select(r => r.Clone())],
    };
}

[JsonConverter(typeof(QueryRuleJsonConverter))]
public sealed class QueryRule : QueryNode
{
    /// <summary>The <see cref="QueryFilter.Id"/>, or <c>null</c> before a field is chosen.</summary>
    public string? Id { get; set; }

    /// <summary>The filter's field, written to JSON for other tools. The SQL generator doesn't read it.</summary>
    public string? Field { get; set; }

    public QueryValueType? Type { get; set; }

    public QueryInputType? Input { get; set; }

    public string? Operator { get; set; }

    /// <summary>
    /// A value, a list for operators such as <c>in</c>, or <c>[from, to]</c> for <c>between</c>.
    /// <see cref="Values.RuleValues"/> reads and writes it.
    /// </summary>
    public JsonNode? Value { get; set; }

    /// <summary>
    /// Hidden from the people running the query, but still part of it. Set in design mode;
    /// stored as <c>data.show: false</c> for compatibility with the design-mode fork of jQuery QueryBuilder.
    /// </summary>
    public bool Hidden { get; set; }

    public RuleFlags Flags { get; set; } = RuleFlags.None;

    /// <summary>Points the rule at <paramref name="filter"/> and records its field, type and input.</summary>
    public void ApplyFilter(QueryFilter? filter)
    {
        Id = filter?.Id;
        Field = filter is null ? null : filter.Field ?? filter.Id;
        Type = filter?.Type;
        Input = filter?.EffectiveInput;
    }

    public override QueryRule Clone() => new()
    {
        Id = Id,
        Field = Field,
        Type = Type,
        Input = Input,
        Operator = Operator,
        Value = Value?.DeepClone(),
        Hidden = Hidden,
        Flags = Flags,
        Data = Data?.DeepClone().AsObject(),
    };
}

using System.Diagnostics.CodeAnalysis;
using QueryKrafter.Demo;
using QueryKrafter.Localization;

namespace QueryKrafter.Demo.Blazor.Components.Pages;

public partial class Home
{
    private QueryGroup rules = QueryJson.Parse(SampleData.StarterRules);
    private bool designMode = true;
    private IReadOnlyList<Order> matches = [];
    private string? sql;
    private string json = string.Empty;
    private string? error;

    protected override void OnInitialized() => Apply(new QueryChange(rules, []));

    private void OnChanged(QueryChange change) => Apply(change);

    [RequiresUnreferencedCode(QueryExpressionBuilder.ReflectionMessage)]
    [RequiresDynamicCode(QueryExpressionBuilder.ReflectionMessage)]
    private void Apply(QueryChange change)
    {
        json = change.ToJson(indented: true);
        if (!change.IsValid)
        {
            matches = [];
            sql = string.Join("\n", change.Errors.Select(e => e.Message(QueryKrafterText.English)));
            error = null;
            return;
        }

        var generated = SqlGenerator.Generate(change.Rules, SampleData.Config, new() { Dialect = SqlDialect.Sqlite, ParameterStyle = SqlParameterStyle.Inline });
        sql = generated.IsSuccess ? generated.Value.Sql : string.Join("\n", generated.Errors);

        var filtered = SampleData.Orders.ApplyQuery(change.Rules, SampleData.Config);
        if (filtered.IsSuccess)
        {
            matches = [.. filtered.Value];
            error = null;
        }
        else
        {
            matches = [];
            error = string.Join("\n", filtered.Errors);
        }
    }
}

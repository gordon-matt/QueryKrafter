using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace QueryKrafter.Demo.Mvc.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        ViewData["Rules"] = SampleData.StarterRules;
        return View(model: null);
    }

    [HttpPost("/")]
    [RequiresUnreferencedCode(QueryExpressionBuilder.ReflectionMessage)]
    [RequiresDynamicCode(QueryExpressionBuilder.ReflectionMessage)]
    public IActionResult Run(string rules)
    {
        ViewData["Rules"] = rules;
        var parsed = QueryJson.TryParse(rules);
        if (!parsed.IsSuccess)
        {
            return View("Index", new RunResult(parsed.ValidationErrors.First().ErrorMessage, null, [], rules));
        }

        var sql = SqlGenerator.Generate(parsed.Value, SampleData.Config, new()
        {
            Dialect = SqlDialect.Sqlite,
            ParameterStyle = SqlParameterStyle.Inline,
        });

        if (!sql.IsSuccess)
        {
            string message = sql.ValidationErrors.FirstOrDefault()?.ErrorMessage ?? sql.Errors.FirstOrDefault() ?? "The rules aren't valid.";
            return View("Index", new RunResult(message, null, [], rules));
        }

        var matches = SampleData.Orders.ApplyQuery(parsed.Value, SampleData.Config);
        return View("Index", new RunResult(
            matches.IsSuccess ? null : string.Join("\n", matches.Errors),
            sql.Value.Sql,
            matches.IsSuccess ? [.. matches.Value] : [],
            rules));
    }
}

public sealed record RunResult(string? Error, string? Sql, IReadOnlyList<Order> Orders, string Rules);

<img src="https://github.com/gordon-matt/QueryKrafter/blob/master/_Misc/Logo.png" alt="Logo" width="450" />

A query builder for .NET. People compose rules over fields you define; you turn those rules into SQL or a LINQ predicate on the server, from a configuration you trust.

The same component works in **Blazor**, **ASP.NET Core MVC / Razor Pages**, and, as a custom element, in **React or any plain HTML page**.

Rules are stored in the [jQuery QueryBuilder](https://querybuilder.js.org/) JSON format, including the design-mode fork's `data.show: false` for rules that stay in the query but are hidden from the people running it.

## Projects

| Project | What it is |
| --- | --- |
| `QueryKrafter.Core` | Rules, configuration, validation, SQL, LINQ, and the language catalogs. No UI. |
| `QueryKrafter.Components` | The `QueryBuilder` Razor component and the `QueryBuilderElement` string-parameter wrapper. |
| `QueryKrafter.CustomElements` | A Blazor WebAssembly app that registers `<qk-query-builder>`. |
| `QueryKrafter.Demo.Blazor` | Blazor Server demo that filters a list of orders. |
| `QueryKrafter.Demo.Mvc` | MVC demo. The view posts the rules; the controller generates the SQL. |
| `querykrafter.demo.react` | React (Vite) demo using the custom element. |
| `QueryKrafter.Tests` | xUnit tests for JSON, validation, SQL (including SQLite), LINQ, and languages. |

## Blazor

```csharp
builder.Services.AddQueryKrafter();
```

```html
<link rel="stylesheet" href="@Assets["_content/QueryKrafter.Components/querykrafter.css"]" />
```

```razor
<QueryBuilder Config="config" @bind-Rules="rules" OnChanged="OnChanged" DesignMode="designMode" />
```

`Config` is yours and stays on the server. Generate SQL or LINQ from it there:

```csharp
SqlGenerator.Generate(rules, config, new() { Dialect = SqlDialect.SqlServer });
orders.ApplyQuery(rules, config);
```

The generator looks each rule up by `id` in `config.Filters` and writes `QueryFilter.Field`. It never uses the `field` that arrived in the rules JSON.

## MVC

Render `QueryBuilderElement` with `render-mode="Server"`. Set `param-Name` so the rules JSON is posted with the form, then call `SqlGenerator.Generate` in the action. See `QueryKrafter.Demo.Mvc`.

## React and plain HTML

Publish `QueryKrafter.CustomElements` and serve its `wwwroot`. The page needs the stylesheet, then:

```html
<qk-query-builder id="builder" design-mode="true" sql-dialect="sqlite"></qk-query-builder>
<script src="_framework/blazor.webassembly.js"></script>
<script>
  builder.setAttribute('config', JSON.stringify({ filters: [{ id: 'total', type: 'double' }] }));
  builder.addEventListener('qk-changed', e => console.log(e.detail));
</script>
```

`e.detail` is `{ rules, isValid, errors, sql }`. `sql` is a preview from the configuration in the page. Generate the SQL you execute on the server, from your own configuration.

Attributes are kebab-case: `design-mode`, `read-only`, `sql-dialect`, `parameter-style`, `show-errors`, `language`, `theme` (`light`, `dark`, `auto`, `bootstrap`).

## Design mode

`DesignMode` shows a hide button on each rule. A hidden rule is stored as `data.show: false`, is still validated, and is still part of the SQL and the LINQ predicate. Outside design mode it is not shown, and a group whose rules are all hidden cannot be edited.

## Languages

English, German, Spanish, French, Brazilian Portuguese, and Simplified Chinese are built in. `AddQueryKrafter(o => o.Directory = "Locales")` loads extra `{tag}.json` files, which override the built-in strings key by key.

## License

Apache-2.0

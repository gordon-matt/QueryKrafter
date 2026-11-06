using QueryKrafter.Components.Infrastructure;
using QueryKrafter.Sql;
using Dialect = QueryKrafter.Sql.SqlDialect;

namespace QueryKrafter.Components;

/// <summary>
/// <see cref="QueryBuilder"/> with string and boolean parameters, so it can be registered as the
/// <c>&lt;qk-query-builder&gt;</c> custom element or rendered from an MVC view. The configuration
/// and rules are JSON. Every change raises the <c>qk-changed</c> DOM event, and with
/// <see cref="Name"/> set the rules are also posted with the surrounding form.
/// </summary>
public sealed partial class QueryBuilderElement : IAsyncDisposable
{
    private QueryKrafterInterop interop = default!;
    private ElementReference host;
    private QueryBuilder? builder;
    private bool isAttached;
    private bool dispatchAfterRender;

    private QueryBuilderConfig config = new();
    private string? configError;
    private string? appliedConfig;
    private QueryGroup? rules;
    private string? appliedRules;
    private string rulesJson = string.Empty;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>A <see cref="QueryBuilderConfig"/> as JSON: <c>{ "filters": [ { "id": "price", "type": "double" } ] }</c>.</summary>
    [Parameter] public string? Config { get; set; }

    /// <summary>Rules in the jQuery QueryBuilder JSON format. Changing it replaces the rules being edited.</summary>
    [Parameter] public string? Rules { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is design mode.
    /// </summary>
    [Parameter] public bool DesignMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is read only.
    /// </summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary><c>light</c>, <c>dark</c>, <c>auto</c> or <c>bootstrap</c>.</summary>
    [Parameter] public string Theme { get; set; } = Constants.Themes.Light;

    /// <summary>
    /// Gets or sets the language.
    /// </summary>
    [Parameter] public string? Language { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is show errors.
    /// </summary>
    [Parameter] public bool ShowErrors { get; set; } = true;

    /// <summary>Shows errors on untouched rules too, e.g. once the user has tried to submit.</summary>
    [Parameter] public bool ShowAllErrors { get; set; }

    /// <summary>
    /// Adds a SQL preview to the <c>qk-changed</c> event in this dialect: <c>ansi</c>,
    /// <c>sqlserver</c>, <c>postgresql</c>, <c>mysql</c> or <c>sqlite</c>. For display only: the
    /// configuration came from the page, so generate the SQL you run on the server.
    /// </summary>
    [Parameter] public string? SqlDialect { get; set; }

    /// <summary><c>named</c> (the default), <c>positional</c>, <c>numbered</c> or <c>inline</c>.</summary>
    [Parameter] public string? ParameterStyle { get; set; }

    /// <summary>Renders a hidden input with this name holding the rules JSON, for posting with a form.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the class.
    /// </summary>
    [Parameter] public string? Class { get; set; }

    /// <inheritdoc/>
    protected override void OnInitialized() => interop = new QueryKrafterInterop(JS);

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (!string.Equals(Config, appliedConfig, StringComparison.Ordinal))
        {
            appliedConfig = Config;
            dispatchAfterRender = true;
            try
            {
                config = string.IsNullOrWhiteSpace(Config) ? new QueryBuilderConfig() : QueryJson.ParseConfig(Config);
                configError = null;
            }
            catch (JsonException ex)
            {
                configError = "The config attribute isn't a valid query builder configuration: " + ex.Message;
            }
        }

        if (!string.Equals(Rules, appliedRules, StringComparison.Ordinal))
        {
            appliedRules = Rules;
            var parsed = QueryJson.TryParse(Rules);

            // A host echoing back the rules it was just sent must not reset the builder mid-edit.
            bool isEcho = parsed.IsSuccess && builder is not null
                && JsonNode.DeepEquals(QueryJson.ToJson(parsed.Value), QueryJson.ToJson(builder.Root));

            if (!isEcho)
            {
                rules = parsed.IsSuccess && !string.IsNullOrWhiteSpace(Rules) ? parsed.Value : null;
                dispatchAfterRender = true;
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            isAttached = true;
        }

        if (dispatchAfterRender && builder is not null)
        {
            dispatchAfterRender = false;
            await PublishAsync(new QueryChange(builder.Root, builder.Errors));
        }
    }

    private Task OnChangedAsync(QueryChange change) => PublishAsync(change);

    private async Task PublishAsync(QueryChange change)
    {
        string json = change.ToJson();
        if (!string.Equals(json, rulesJson, StringComparison.Ordinal))
        {
            rulesJson = json;
            if (!string.IsNullOrEmpty(Name))
            {
                StateHasChanged();
            }
        }

        if (!isAttached)
        {
            return;
        }

        try
        {
            await interop.DispatchAsync(host, Constants.DomEvents.Changed, Detail(change).ToJsonString());
        }
        catch (JSDisconnectedException)
        {
            // The page is gone; nobody is listening.
        }
    }

    /// <summary>
    /// Built as JSON nodes rather than serialized from a type, so it needs no reflection in a trimmed build:
    /// <c>{ rules, isValid, errors: [{ path, code, message }], sql: { text, parameters: [{ name, value }] } }</c>.
    /// </summary>
    private JsonObject Detail(QueryChange change)
    {
        var errors = new JsonArray();
        foreach (var error in change.Errors)
        {
            JsonNode item = new JsonObject
            {
                ["path"] = error.Path,
                ["code"] = error.Code,
                ["message"] = builder?.Message(error) ?? error.Code,
            };
            errors.Add(item);
        }

        return new JsonObject
        {
            ["rules"] = QueryJson.ToJson(change.Rules),
            ["isValid"] = change.IsValid,
            ["errors"] = errors,
            ["sql"] = Sql(change),
        };
    }

    private JsonObject? Sql(QueryChange change)
    {
        if (string.IsNullOrWhiteSpace(SqlDialect) || !change.IsValid)
        {
            return null;
        }

        var options = new SqlGeneratorOptions
        {
            Dialect = Dialect.FromName(SqlDialect) ?? Dialect.Ansi,
            ParameterStyle = Enum.TryParse(ParameterStyle, ignoreCase: true, out SqlParameterStyle style) ? style : SqlParameterStyle.Named,
        };

        var result = SqlGenerator.Generate(change.Rules, config, options);
        if (!result.IsSuccess)
        {
            return null;
        }

        var parameters = new JsonArray();
        foreach (var parameter in result.Value.Parameters)
        {
            JsonNode item = new JsonObject
            {
                ["name"] = parameter.Name,
                ["value"] = ToNode(parameter.Value),
            };
            parameters.Add(item);
        }

        return new JsonObject
        {
            ["text"] = result.Value.Sql,
            ["parameters"] = parameters,
        };
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        string text => JsonValue.Create(text),
        long number => JsonValue.Create(number),
        decimal number => JsonValue.Create(number),
        double number => JsonValue.Create(number),
        bool flag => JsonValue.Create(flag),
        DateTime date => JsonValue.Create(date.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture)),
        TimeSpan time => JsonValue.Create(time.ToString("c", CultureInfo.InvariantCulture)),
        null => null,
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    /// <summary>
    /// Releases the resources used by this instance.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await interop.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or InvalidOperationException or ObjectDisposedException or TaskCanceledException)
        {
            // The circuit or page is already gone.
        }
    }
}

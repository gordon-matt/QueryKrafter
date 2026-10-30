using Microsoft.Extensions.DependencyInjection;
using QueryKrafter.Components.Infrastructure;

namespace QueryKrafter.Components;

/// <summary>
/// Builds a tree of rules from the filters in <see cref="Config"/>. In <see cref="DesignMode"/>
/// each rule can be hidden from the people who later run the query; hidden rules still take part
/// in it, and outside design mode they aren't shown and groups protecting them can't be changed.
/// </summary>
public sealed partial class QueryBuilder : IAsyncDisposable
{
    private readonly HashSet<QueryNode> touched = new(ReferenceEqualityComparer.Instance);
    private Dictionary<QueryNode, QueryError> errorsByNode = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<QueryError> errors = [];

    private QueryKrafterInterop interop = default!;
    private ElementReference rootElement;

    private QueryGroup root = new();
    private QueryGroup? appliedRules;
    private QueryBuilderConfig? appliedConfig;
    private string? appliedLanguage;
    private bool isInitialized;
    private bool validated;
    private QueryNode? dragged;
    private QueryKrafterText text = QueryKrafterText.English;
    private IReadOnlyList<OptionSection<QueryFilter>> filterSections = [];

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Inject] private IServiceProvider Services { get; set; } = default!;

    [Parameter, EditorRequired] public QueryBuilderConfig Config { get; set; } = new();

    /// <summary>The tree to edit, in place. Without it the builder starts with one empty rule.</summary>
    [Parameter] public QueryGroup? Rules { get; set; }

    [Parameter] public EventCallback<QueryGroup> RulesChanged { get; set; }

    /// <summary>Raised after every edit, with the rules and their validation errors.</summary>
    [Parameter] public EventCallback<QueryChange> OnChanged { get; set; }

    /// <summary>Lets rules be hidden from the people who will run the query.</summary>
    [Parameter] public bool DesignMode { get; set; }

    [Parameter] public bool ReadOnly { get; set; }

    /// <summary><c>light</c>, <c>dark</c>, <c>auto</c> (follow the OS) or <c>bootstrap</c> (follow the page's Bootstrap 5.3 colours).</summary>
    [Parameter] public string Theme { get; set; } = Constants.Themes.Light;

    /// <summary>IETF language tag, such as <c>fr-CA</c>. Defaults to the UI culture.</summary>
    [Parameter] public string? Language { get; set; }

    /// <summary>Shows each rule's errors once the user has changed it, and every error after <see cref="Validate"/>.</summary>
    [Parameter] public bool ShowErrors { get; set; } = true;

    /// <summary>Shows errors on rules the user hasn't touched yet too, as <see cref="Validate"/> does.</summary>
    [Parameter] public bool ShowAllErrors { get; set; }

    [Parameter] public string? Class { get; set; }

    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The rules being edited.</summary>
    public QueryGroup Root => root;

    public IReadOnlyList<QueryError> Errors => errors;

    public bool IsValid => errors.Count == 0;

    internal QueryKrafterText Text => text;

    internal bool IsDragging => dragged is not null;

    internal IReadOnlyList<OptionSection<QueryFilter>> FilterSections => filterSections;

    private IQueryKrafterLocalizer Locales => Services.GetService<IQueryKrafterLocalizer>() ?? QueryKrafterLocaleCatalog.Shared;

    /// <summary>Errors on rules the people running the query can't see, so can't fix.</summary>
    private int HiddenErrorCount => DesignMode || !ShowErrors ? 0 : errors.Count(e => !IsShown(e.Node));

    protected override void OnInitialized() => interop = new QueryKrafterInterop(JS);

    protected override void OnParametersSet()
    {
        string? language = string.IsNullOrWhiteSpace(Language) ? CultureInfo.CurrentUICulture.Name : Language;
        bool languageChanged = !string.Equals(language, appliedLanguage, StringComparison.OrdinalIgnoreCase);
        if (languageChanged)
        {
            appliedLanguage = language;
            text = Locales.Resolve(language);
        }

        if (!ReferenceEquals(Config, appliedConfig) || languageChanged)
        {
            appliedConfig = Config;
            filterSections = OptionSection<QueryFilter>.Build(
                Config.Filters.Where(filter => !filter.HideFromPicker).ToList(),
                f => f.Group,
                f => f.DisplayLabel,
                Config.SortFilters,
                text.Culture);
        }

        // The host binding back the tree raised through RulesChanged must not reset the builder.
        if (!isInitialized || (!ReferenceEquals(Rules, root) && !ReferenceEquals(Rules, appliedRules)))
        {
            isInitialized = true;
            appliedRules = Rules;
            root = Rules ?? CreateRoot();
            touched.Clear();
            validated = false;
        }

        Revalidate();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await interop.InitDragDropAsync(rootElement);
        }
    }

    /// <summary>Shows every error, including on rules the user hasn't touched.</summary>
    /// <returns>Whether the rules are valid.</returns>
    public bool Validate()
    {
        validated = true;
        Revalidate();
        StateHasChanged();
        return IsValid;
    }

    /// <summary>Starts again with one empty rule, or none when the configuration allows an empty query.</summary>
    public async Task ResetAsync()
    {
        root = CreateRoot();
        touched.Clear();
        validated = false;
        await NotifyAsync();
    }

    public async Task SetRulesAsync(QueryGroup rules)
    {
        root = rules;
        appliedRules = rules;
        touched.Clear();
        validated = false;
        await NotifyAsync();
    }

    public string ToJson(bool indented = false) => QueryJson.Serialize(root, indented);

    // ----------------------------------------------------------------- state for the parts

    internal QueryError? VisibleError(QueryNode node) =>
        ShowErrors && (validated || ShowAllErrors || touched.Contains(node)) ? errorsByNode.GetValueOrDefault(node) : null;

    internal string Message(QueryError error) =>
        error.Message(text, error.Node is QueryRule rule ? Config.FindFilter(rule.Id)?.Validation : null);

    /// <summary>Hidden rules, and groups holding nothing else, are only shown in design mode.</summary>
    internal bool IsVisible(QueryNode node) => DesignMode || node switch
    {
        QueryRule rule => !rule.Hidden,
        QueryGroup group => ReferenceEquals(group, root) || group.Rules.Count == 0 || group.Rules.Any(IsVisible),
        _ => true,
    };

    /// <summary>
    /// Outside design mode a group holding hidden rules keeps its logic, so the people running
    /// the query can't undo the conditions they can't see.
    /// </summary>
    internal bool IsLogicLocked(QueryGroup group) =>
        ReadOnly || group.Flags.ConditionReadOnly || (!DesignMode && group.AllRules().Any(r => r.Hidden));

    internal bool CanAddRule(QueryGroup group) => !ReadOnly && !group.Flags.NoAddRule;

    internal bool CanAddGroup(QueryGroup group, int level) => !ReadOnly && !group.Flags.NoAddGroup && Config.CanAddGroupAt(level);

    internal bool CanDelete(QueryNode node) => !ReadOnly && node switch
    {
        QueryRule rule => !rule.Flags.NoDelete,
        QueryGroup group => !ReferenceEquals(group, root) && !group.Flags.NoDelete && !IsLogicLocked(group),
        _ => false,
    };

    internal bool CanDrag(QueryNode node) =>
        Config.AllowSorting && !ReadOnly && !ReferenceEquals(node, root) && (node is not QueryGroup group || !IsLogicLocked(group));

    /// <summary>Whether another rule already uses a filter that may only be used once.</summary>
    internal bool IsFilterTaken(QueryFilter filter, QueryRule rule)
    {
        if (filter.Unique == UniqueScope.None)
        {
            return false;
        }

        var scope = filter.Unique == UniqueScope.Group
            ? (root.ParentOf(rule)?.Rules.OfType<QueryRule>() ?? [])
            : root.AllRules();

        return scope.Any(other => !ReferenceEquals(other, rule) && string.Equals(other.Id, filter.Id, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------- edits

    internal Task AddRuleAsync(QueryGroup group)
    {
        group.Rules.Add(CreateRule());
        return NotifyAsync(group);
    }

    internal Task AddGroupAsync(QueryGroup group)
    {
        group.Rules.Add(CreateGroup());
        return NotifyAsync(group);
    }

    internal Task DeleteAsync(QueryNode node)
    {
        if (root.ParentOf(node) is not { } parent)
        {
            return Task.CompletedTask;
        }

        parent.Rules.Remove(node);
        return NotifyAsync(parent);
    }

    internal Task SetConditionAsync(QueryGroup group, string condition)
    {
        group.Condition = condition;
        return NotifyAsync(group);
    }

    internal Task ToggleNotAsync(QueryGroup group)
    {
        group.Not = !group.Not;
        return NotifyAsync(group);
    }

    /// <summary>Inverts in place, so the group keeps its identity and place in the tree.</summary>
    internal Task InvertAsync(QueryGroup group)
    {
        var result = QueryInverter.Invert(group, Config);
        if (!result.IsSuccess)
        {
            return Task.CompletedTask;
        }

        group.Condition = result.Value.Condition;
        group.Not = result.Value.Not;
        group.Rules = result.Value.Rules;
        return NotifyAsync(group);
    }

    /// <summary>Keeps the operator when the new filter offers it; the value starts again from the filter's default.</summary>
    internal Task ChangeFilterAsync(QueryRule rule, string? filterId)
    {
        var filter = Config.FindFilter(filterId);
        rule.ApplyFilter(filter);
        rule.Data = filter?.Data?.DeepClone().AsObject();

        if (filter is null)
        {
            rule.Operator = null;
            rule.Value = null;
            return NotifyAsync(rule);
        }

        var operators = Config.OperatorsFor(filter);
        var op = operators.FirstOrDefault(o => string.Equals(o.Type, rule.Operator, StringComparison.Ordinal))
            ?? Config.DefaultOperatorFor(filter);
        rule.Operator = op?.Type;
        rule.Value = op is { InputCount: > 0 } ? filter.DefaultValue?.DeepClone() : null;
        return NotifyAsync(rule);
    }

    internal Task ChangeOperatorAsync(QueryRule rule, string? type)
    {
        var previous = Config.FindOperator(rule.Operator);
        var next = Config.FindOperator(type);
        rule.Operator = next?.Type;

        if (next is null || next.InputCount == 0)
        {
            rule.Value = null;
        }
        else if (!RuleValues.Fits(previous, next))
        {
            rule.Value = next.InputCount == 1 && !next.Multiple ? Config.FindFilter(rule.Id)?.DefaultValue?.DeepClone() : null;
        }

        return NotifyAsync(rule);
    }

    internal Task ToggleHiddenAsync(QueryRule rule)
    {
        rule.Hidden = !rule.Hidden;
        return NotifyAsync(rule);
    }

    /// <summary>Called by the value editor after it has written the rule's value.</summary>
    internal Task ValueChangedAsync(QueryRule rule) => NotifyAsync(rule);

    // -------------------------------------------------------------------- drag and drop

    internal void StartDrag(QueryNode node) => dragged = node;

    internal void EndDrag()
    {
        if (dragged is not null)
        {
            dragged = null;
            StateHasChanged();
        }
    }

    /// <summary>Whether the node being dragged may go into <paramref name="target"/>, a group at <paramref name="level"/>.</summary>
    internal bool CanDropInto(QueryGroup target, int level)
    {
        switch (dragged)
        {
            case QueryRule:
                return !target.Flags.NoAddRule;

            case QueryGroup group:
                if (ReferenceEquals(group, target) || group.Contains(target) || target.Flags.NoAddGroup)
                {
                    return false;
                }

                return Config.CanAddGroupAt(level + Depth(group) - 1);

            default:
                return false;
        }
    }

    internal Task DropAsync(QueryGroup target, int index)
    {
        var node = dragged;
        dragged = null;

        if (node is null || root.ParentOf(node) is not { } parent)
        {
            return Task.CompletedTask;
        }

        int from = parent.Rules.IndexOf(node);
        parent.Rules.RemoveAt(from);
        if (ReferenceEquals(parent, target) && from < index)
        {
            index--;
        }

        target.Rules.Insert(Math.Clamp(index, 0, target.Rules.Count), node);
        return NotifyAsync(node);
    }

    // ---------------------------------------------------------------------- internals

    private async Task NotifyAsync(QueryNode? changed = null)
    {
        if (changed is not null)
        {
            touched.Add(changed);
        }

        Revalidate();
        StateHasChanged();
        await RulesChanged.InvokeAsync(root);
        await OnChanged.InvokeAsync(new QueryChange(root, errors));
    }

    private void Revalidate()
    {
        errors = QueryValidator.Validate(root, Config, text: text);
        var byNode = new Dictionary<QueryNode, QueryError>(ReferenceEqualityComparer.Instance);
        foreach (var error in errors)
        {
            byNode.TryAdd(error.Node, error);
        }

        errorsByNode = byNode;
    }

    /// <summary>Whether a node is on screen: visible itself and inside groups that are.</summary>
    private bool IsShown(QueryNode node)
    {
        if (!IsVisible(node))
        {
            return false;
        }

        var parent = root.ParentOf(node);
        return parent is null || IsShown(parent);
    }

    private QueryGroup CreateRoot() => new()
    {
        Condition = Config.DefaultCondition,
        Flags = Config.DefaultGroupFlags,
        Rules = Config.AllowEmpty ? [] : [CreateRule()],
    };

    private QueryGroup CreateGroup() => new()
    {
        Condition = Config.DefaultCondition,
        Flags = Config.DefaultGroupFlags,
        Rules = [CreateRule()],
    };

    private QueryRule CreateRule()
    {
        var rule = new QueryRule { Flags = Config.DefaultRuleFlags };
        if (Config.FindFilter(Config.DefaultFilter) is { } filter)
        {
            rule.ApplyFilter(filter);
            rule.Data = filter.Data?.DeepClone().AsObject();
            var op = Config.DefaultOperatorFor(filter);
            rule.Operator = op?.Type;
            rule.Value = op is { InputCount: > 0 } ? filter.DefaultValue?.DeepClone() : null;
        }

        return rule;
    }

    private static int Depth(QueryGroup group) => 1 + group.Rules.OfType<QueryGroup>().Select(Depth).DefaultIfEmpty(0).Max();

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

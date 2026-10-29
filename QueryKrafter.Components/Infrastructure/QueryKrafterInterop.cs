namespace QueryKrafter.Components.Infrastructure;

/// <summary>
/// The component's JavaScript, loaded as a module on first use so hosts only need the
/// stylesheet in their page.
/// </summary>
internal sealed class QueryKrafterInterop(IJSRuntime js) : IAsyncDisposable
{
    private const string ModulePath = "./_content/QueryKrafter.Components/querykrafter.js";

    private Task<IJSObjectReference>? module;

    private Task<IJSObjectReference> Module => module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();

    public async ValueTask InitDragDropAsync(ElementReference root) =>
        await (await Module).InvokeVoidAsync("initDragDrop", root);

    /// <summary>Raises a DOM event that bubbles out of the custom element, with a JSON detail so it survives trimming.</summary>
    public async ValueTask DispatchAsync(ElementReference element, string eventName, string detailJson) =>
        await (await Module).InvokeVoidAsync("dispatch", element, eventName, detailJson);

    public async ValueTask DisposeAsync()
    {
        if (module is { IsCompletedSuccessfully: true })
        {
            try
            {
                await module.Result.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already gone, and the module with it.
            }
        }
    }
}

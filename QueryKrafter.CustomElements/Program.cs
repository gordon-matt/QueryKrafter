using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using QueryKrafter.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// <qk-query-builder config='{"filters":[...]}' design-mode="true" sql-dialect="sqlserver"></qk-query-builder>.
// Parameters map to kebab-case attributes; the element raises the qk-changed DOM event.
builder.RootComponents.RegisterCustomElement<QueryBuilderElement>("qk-query-builder");

await builder.Build().RunAsync();

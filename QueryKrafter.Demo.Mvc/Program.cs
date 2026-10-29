var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// The builder is a Blazor component rendered into the MVC view over a Blazor Server circuit.
builder.Services.AddServerSideBlazor();
builder.Services.AddQueryKrafter();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapControllers();
app.MapBlazorHub();
app.Run();

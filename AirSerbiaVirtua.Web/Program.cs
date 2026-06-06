using AirSerbiaVirtua.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// SSR pages call the VA API server-side (public endpoints, no auth).
// Api:BaseUrl — dev: http://localhost:5036/, docker: http://api:5000/.
builder.Services.AddHttpClient("Api", (sp, client) =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5036/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AirSerbiaVirtua.Web.Client._Imports).Assembly);

app.Run();

using AirSerbiaVirtua.Web.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The portal talks straight to the VA API (CORS allows the web origins).
var apiBase = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5036/";
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });

builder.Services.AddScoped<PortalSession>();
builder.Services.AddScoped<IPortalApi, PortalApi>();

await builder.Build().RunAsync();

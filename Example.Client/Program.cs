using Example.Client.Monitor;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
#if EXAMPLE_STANDALONE
builder.RootComponents.Add<Example.Client.Components.Routes>("#app");
builder.RootComponents.Add<Microsoft.AspNetCore.Components.Web.HeadOutlet>("head::after");
#endif
builder.Services.AddMudServices();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MonitorFeed>();

await builder.Build().RunAsync();

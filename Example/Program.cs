using Example.Components;
using Example.Monitor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MonitorFeed>();
builder.Services.AddHostedService(static p => p.GetRequiredService<MonitorFeed>());

var app = builder.Build();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

namespace Example.Application;

using Example.Components;
using Example.Monitor;

using MudBlazor.Services;

public static class ApplicationExtensions
{
    //--------------------------------------------------------------------------------
    // Blazor
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureBlazor(this IHostApplicationBuilder builder)
    {
        // Razor components
        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = builder.Environment.IsDevelopment();
            });

        // MudBlazor
        builder.Services.AddMudServices();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Monitor
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureMonitor(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<MonitorFeed>();
        builder.Services.AddHostedService(static p => p.GetRequiredService<MonitorFeed>());

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Error handler
    //--------------------------------------------------------------------------------

    public static WebApplication UseErrorHandler(this WebApplication app)
    {
        app.UseExceptionHandler("/error", createScopeForErrors: true);
        app.UseWhen(
            static context => HttpMethods.IsGet(context.Request.Method),
            static b => b.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));

        return app;
    }

    //--------------------------------------------------------------------------------
    // End point
    //--------------------------------------------------------------------------------

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        // Static assets
        app.MapStaticAssets();

        // Blazor
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}

namespace Example.Application;

using Example.Client.Components;
using Example.Client.Models;
using Example.Components;
using Example.Endpoints;

using Microsoft.AspNetCore.Localization;

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
            })
            .AddInteractiveWebAssemblyComponents();

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
        builder.Services.AddScoped<MonitorFeed>();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Localization
    //--------------------------------------------------------------------------------

    public static WebApplication UseLocalization(this WebApplication app)
    {
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture("en-US"),
            SupportedCultures = [CultureInfo.GetCultureInfo("en-US")],
            SupportedUICultures = [CultureInfo.GetCultureInfo("en-US")]
        });

        return app;
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
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Routes).Assembly);

        // Settings
        app.MapSettingEndpoints();

        return app;
    }
}

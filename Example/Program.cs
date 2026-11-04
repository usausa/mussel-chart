using Example.Application;

//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

// Blazor
builder.ConfigureBlazor();

// Monitor
builder.ConfigureMonitor();

//--------------------------------------------------------------------------------
// Configure the HTTP request pipeline
//--------------------------------------------------------------------------------
var app = builder.Build();

// Error handler
app.UseErrorHandler();

// Routing
app.UseRouting();

// Antiforgery
app.UseAntiforgery();

// End point
app.MapEndpoints();

// Run
await app.RunAsync();

using Microsoft.AspNetCore.Components;
using Pokerface.Components;
using Pokerface.Pages;
using Pokerface.Services;
using Pokerface.Services.DB;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// Fallback for command-line args
builder.Configuration.AddCommandLine(args);

// DB path must come from startup arguments
var dbPath = builder.Configuration["DB_PATH"] ?? throw new InvalidOperationException("DB_PATH is missing");

// Services
builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();

// Add Razor Components (Blazor Server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database service
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new BaseDataBase("Table.db", dbPath);
});

// Other services
builder.Services.AddSingleton<DbTableService>();
builder.Services.AddSingleton<TableService>();
builder.Services.AddSingleton<GameSessionService>();

var app = builder.Build();

// Initialize databases during startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var tableDB = services.GetRequiredService<DbTableService>();
    tableDB.Init().Wait();
}

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();

var pathBase = builder.Configuration["PATH_BASE"];
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

app.UseRouting();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();

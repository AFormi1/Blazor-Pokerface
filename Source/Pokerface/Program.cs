using Pokerface.Components;
using Pokerface.Services;
using Pokerface.Services.DB;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<CardProvider>();
builder.Services.AddSingleton<DbTableService>();
builder.Services.AddSingleton<TableService>();

var app = builder.Build();



// Initialize databases during startup and create the default database if not exists (playground)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var tableDB = services.GetRequiredService<DbTableService>();

    tableDB.Init().Wait();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

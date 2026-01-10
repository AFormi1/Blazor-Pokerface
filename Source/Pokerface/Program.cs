using Pokerface.Components;
using Pokerface.Services;
using Pokerface.Services.DB;

var builder = WebApplication.CreateBuilder(args);

// fallback for event args
builder.Configuration.AddCommandLine(args);

//the dbPath must come from the startup argumens - is required
var dbPath = builder.Configuration["DB_PATH"] ?? throw new InvalidOperationException("DB_PATH is missing");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

//apply the arguments with dbPath to the sevice
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new BaseDataBase("Table.db", dbPath);
});

builder.Services.AddSingleton<DbTableService>();


builder.Services.AddSingleton<TableService>();
builder.Services.AddSingleton<GameSessionService>();

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

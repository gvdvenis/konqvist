using Konqvist.Data;
using Konqvist.Data.Infrastructure;
using Konqvist.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;
using Konqvist.Web.Core;

var builder = WebApplication.CreateBuilder(args);

// --- Gameplay state persistence (Azure SQL) configuration ---
// Required connection string for the GameplayStateDatabase. Set via
// user-secrets or environment variable; never commit credentials.
var gameplayStateConnectionString = builder.Configuration.GetConnectionString("GameplayStateDatabase");
if (string.IsNullOrWhiteSpace(gameplayStateConnectionString))
{
    throw new InvalidOperationException(
        "Required connection string 'ConnectionStrings:GameplayStateDatabase' is missing or empty. " +
        "Configure it via user-secrets (e.g. 'dotnet user-secrets set ConnectionStrings:GameplayStateDatabase \"...\"') " +
        "or the GameplayStateDatabase environment variable. Never commit credentials to the repository.");
}

// EF Core DbContext for the Azure SQL gameplay-state persistence layer.
builder.Services.AddDbContext<GameplayStateDbContext>(
    options => options.UseSqlServer(gameplayStateConnectionString));

// Bind and validate gameplay-state persistence options (SaveInterval, Slot).
builder.Services
    .Configure<GameplayStatePersistenceOptions>(
        builder.Configuration.GetSection("GameplayStatePersistence"));

// --- Transition-based gameplay-state write logging (#21) ---
// Singleton: outage state must persist across write calls. Parses the
// allowlisted connection-target fields (DataSource, InitialCatalog, Encrypt)
// once from the connection string; credentials and the full connection
// string are never stored or logged by the logger.
builder.Services.AddSingleton<IGameplayStateWriteLogger>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("GameplayStateDatabase");
    var slot = sp.GetRequiredService<IOptions<GameplayStatePersistenceOptions>>().Value.Slot;
    var logger = sp.GetRequiredService<ILogger<TransitionGameplayStateWriteLogger>>();
    return new TransitionGameplayStateWriteLogger(connectionString, slot, logger);
});


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient();
builder.Services.AddAuthorization();
builder.Services.AddGeolocationServices();
builder.Services.AddScoped<IBindableHubClient, GameHubClient>();
builder.Services.AddScoped<IGameHubClient>(x => x.GetRequiredService<IBindableHubClient>());
builder.Services.AddScoped<SessionProvider>();
builder.Services.AddScoped<GameModeRoutingService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddSingleton<SessionKeyProvider>();
builder.Services.AddSingleton<IMapDataLoader, MapDataLoader>();
builder.Services.AddSingleton<IGameplayStateStore>(sp =>
{
    // Register the SQL-backed gameplay-state store as a lazily-initialized
    // adapter. SqlGameplayStateStore needs the GameDefinitionId, which comes
    // from MapDataStore.GameDefinitionHash; but MapDataStore's constructor
    // takes IGameplayStateStore?, so resolving the SQL store eagerly to satisfy
    // MapDataStore would form a DI cycle. LazyGameplayStateStore defers the
    // resolution of MapDataStore (and the DbContext) to the first
    // Read/Write/Clear call, by which time the DI graph is fully built and
    // MapDataStore.InitializeAsync() has computed the hash. Persistence is then
    // scoped to (Slot, GameDefinitionId): Slot from config, GameDefinitionId
    // from MapDataStore.GameDefinitionHash. (#19)
    var slot = sp.GetRequiredService<IOptions<GameplayStatePersistenceOptions>>().Value.Slot;
    return new LazyGameplayStateStore(sp, slot);
});
builder.Services.AddSingleton<MapDataStore>();

// Buffered, coalesced gameplay-state writer (#18). Wraps the
// IGameplayStateStore and coalesces burst mutations into at most one write
// per configured interval. Registered as a singleton so MapDataStore can
// route PersistGameplayState through ScheduleSave.
builder.Services.AddSingleton<BufferedGameplayStateWriter>(sp =>
{
    var store = sp.GetRequiredService<IGameplayStateStore>();
    var options = sp.GetRequiredService<IOptions<GameplayStatePersistenceOptions>>();
    var logger = sp.GetRequiredService<ILogger<BufferedGameplayStateWriter>>();
    var writeLogger = sp.GetRequiredService<IGameplayStateWriteLogger>();
    return new BufferedGameplayStateWriter(store, options, logger, writeLogger: writeLogger);
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddFluentUIComponents();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Konqvist.Auth";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.MaxAge = TimeSpan.FromHours(3);
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.Events = new CookieAuthenticationEvents()
        {
            OnRedirectToLogin = AuthenticationHelpers.StripRedirectUrlParams,
            OnValidatePrincipal = AuthenticationHelpers.ValidateSessionKey
        };
    });

// configure Kestrel to use our dev certificate for network access during local development
builder.AddLocalDevCertificate(7040);

var app = builder.Build();

// --- Validate gameplay-state persistence options at startup (fail-fast) ---
var persistenceOptions = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<GameplayStatePersistenceOptions>>()
    .Value;
if (!persistenceOptions.IsValid(out var persistenceOptionsError))
{
    throw new InvalidOperationException(persistenceOptionsError);
}

// --- Apply EF Core migrations and verify database availability at startup ---
using (var scope = app.Services.CreateScope())
{
    var dbLogger = scope.ServiceProvider
        .GetRequiredService<ILogger<GameplayStateDbContext>>();
    var context = scope.ServiceProvider
        .GetRequiredService<GameplayStateDbContext>();

    try
    {
        if (!await context.Database.CanConnectAsync())
        {
            throw new InvalidOperationException(
                "Unable to connect to the GameplayStateDatabase. Check the " +
                "'ConnectionStrings:GameplayStateDatabase' configuration, network " +
                "access, and that the Azure SQL database exists and is reachable.");
        }
    }
    catch (Exception ex) when (ex is not InvalidOperationException)
    {
        throw new InvalidOperationException(
            "Failed to reach the GameplayStateDatabase during startup availability " +
            "check. See the inner exception for details.", ex);
    }

    try
    {
        dbLogger.LogInformation(
            "Applying EF Core migrations for GameplayStateDbContext...");
        await context.Database.MigrateAsync();
        dbLogger.LogInformation(
            "EF Core migrations for GameplayStateDbContext applied successfully.");
    }
    catch (Exception ex)
    {
        dbLogger.LogError(ex,
            "Failed to apply EF Core migrations for GameplayStateDbContext at startup.");
        throw new InvalidOperationException(
            "Failed to apply EF Core migrations for the GameplayStateDatabase at " +
            "startup. The application will not start until migrations can be applied. " +
            "See the inner exception for details.", ex);
    }
}

// Initialize the main application datastore
var mapDataSource = app.Services.GetRequiredService<MapDataStore>();

await mapDataSource.InitializeAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    //KmlToMapDataConverter.Run(
    //    @"D:\Source\konqvist\Konqvist.Data\Data\Konqvist.kml",
    //    @"D:\Source\konqvist\Konqvist.Data\Data\map.json");
}

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<GameHubServer>(GameHubServer.HubUrl);

// Graceful shutdown: flush any pending buffered gameplay state before the
// host stops, bounded by the configured 5-second shutdown flush timeout (#18/#15).
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var bufferedWriter = app.Services.GetRequiredService<BufferedGameplayStateWriter>();
lifetime.ApplicationStopping.Register(() =>
{
    bufferedWriter.ShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();
});

app.Run();

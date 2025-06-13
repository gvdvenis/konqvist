using Konqvist.Data;
using Konqvist.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.FluentUI.AspNetCore.Components;
using Konqvist.Web.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents();
builder.Services.AddAuthorization();
builder.Services.AddGeolocationServices();
builder.Services.AddScoped<IBindableHubClient, GameHubClient>();
builder.Services.AddScoped<IGameHubClient>(x => x.GetRequiredService<IBindableHubClient>());
builder.Services.AddScoped<SessionProvider>();
builder.Services.AddScoped<GameModeRoutingService>();
builder.Services.AddSingleton<SessionKeyProvider>();
builder.Services.AddSingleton<IMapDataLoader, MapDataLoader>();
builder.Services.AddSingleton<MapDataStore>();
builder.Services.AddCascadingAuthenticationState();
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

app.Run();

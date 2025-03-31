
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

//builder.AddRedisOutputCache("cache");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

builder.Services.AddScoped<IBindableHubClient, GameHubClient>();
builder.Services.AddScoped<IGameHubClient>(x => x.GetRequiredService<IBindableHubClient>());

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ElectionGame.Auth";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.MaxAge = TimeSpan.FromHours(3);
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";

    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddGeolocationServices();
builder.Services.AddScoped<SessionProvider>();

// Add to your existing service registration section
builder.Services.AddSingleton(_ =>  MapDataStore.GetInstanceAsync().GetAwaiter().GetResult());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseAuthentication();

app.UseAuthorization();

app.UseHttpsRedirection();

app.UseAntiforgery();

//app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<GameHubServer>(GameHubServer.HubUrl);

app.Run();

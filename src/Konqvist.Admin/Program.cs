using System.Security.Claims;
using FluentValidation;
using Konqvist.Admin.Components;
using Konqvist.Admin.Features.Auth;
using Konqvist.Admin.Features.Teams;
using Konqvist.Admin.Features.Templates;
using Konqvist.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

builder.Services.AddMudServices();
builder.Services.AddDbContextFactory<KonqvistDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddScoped<GameTemplateAdminService>();
builder.Services.AddScoped<TeamTemplateAdminService>();
builder.Services.AddScoped<IValidator<CreateGameTemplateInput>, CreateGameTemplateInputValidator>();
builder.Services.AddScoped<IValidator<TeamTemplateEditorInput>, TeamTemplateEditorInputValidator>();

builder.Services
    .AddOptions<AdminCredentialsOptions>()
    .Bind(builder.Configuration.GetSection(AdminCredentialsOptions.SectionName))
    .Validate(
        credentials => !string.IsNullOrWhiteSpace(credentials.Username),
        $"{AdminCredentialsOptions.SectionName}:Username is required.")
    .Validate(
        credentials => !string.IsNullOrWhiteSpace(credentials.Password),
        $"{AdminCredentialsOptions.SectionName}:Password is required.")
    .ValidateOnStart();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/login";
    });
builder.Services.AddAuthorization();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KonqvistDbContext>>();
    await using var dbContext = await dbContextFactory.CreateDbContextAsync();
    await dbContext.Database.MigrateAsync();
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

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var requestPath = context.Request.Path.Value ?? string.Empty;
    var isAdminPath = requestPath.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);
    var isLoginPath = requestPath.Equals("/admin/login", StringComparison.OrdinalIgnoreCase);
    var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;

    if (isAdminPath && !isLoginPath && !isAuthenticated)
    {
        await context.ChallengeAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return;
    }

    if (isLoginPath && HttpMethods.IsGet(context.Request.Method) && isAuthenticated)
    {
        context.Response.Redirect("/admin");
        return;
    }

    await next();
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapPost(
    "/admin/login",
    async Task<IResult> (
        HttpContext httpContext,
        [FromForm] string username,
        [FromForm] string password,
        [FromForm] string? returnUrl,
        IOptions<AdminCredentialsOptions> credentialsOptions) =>
    {
        var credentials = credentialsOptions.Value;
        var isValidUsername = string.Equals(username, credentials.Username, StringComparison.Ordinal);
        var isValidPassword = string.Equals(password, credentials.Password, StringComparison.Ordinal);

        if (!isValidUsername || !isValidPassword)
        {
            var loginPath = "/admin/login?error=1";
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                loginPath += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            }

            return Results.LocalRedirect(loginPath);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, credentials.Username),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Results.LocalRedirect("/admin");
    });
app.MapPost(
    "/admin/logout",
    async Task<IResult> (HttpContext httpContext) =>
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.LocalRedirect("/admin/login");
    });

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

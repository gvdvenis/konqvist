using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Domain.Aggregates;
using Konqvist.Server.Domain.Persistence;
using Konqvist.Server.Domain.Serialization;
using Konqvist.Server.Features.Auth;
using Konqvist.Server.Features.Admin;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/konqvist-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14));

    builder.Services.AddDbContext<KonqvistDbContext>(
        options => options.UseSqlite(connectionString),
        contextLifetime: ServiceLifetime.Scoped,
        optionsLifetime: ServiceLifetime.Singleton);
    builder.Services.AddDbContextFactory<KonqvistDbContext>(options =>
        options.UseSqlite(connectionString));
    builder.Services
        .AddOptions<AdminAppOptions>()
        .Bind(builder.Configuration.GetSection(AdminAppOptions.SectionName));
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, AuthJsonSerializerContext.Default);
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, GameAggregateJsonSerializerContext.Default);
    });
    builder.Services.AddSingleton<IGameEventWalWriter, EfGameEventWalWriter>();
    builder.Services.AddSingleton<GameAggregate>();
    builder.Services.AddAuthentication(AuthConstants.AuthenticationScheme)
        .AddCookie(AuthConstants.AuthenticationScheme, options =>
        {
            options.Cookie.Name = AuthConstants.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    builder.Services.AddAuthorization();
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:5183", "https://localhost:7133")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
    builder.Services.AddOpenApi();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
        dbContext.Database.Migrate();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    var adminAppOptions = app.Services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminAppOptions>>()
        .Value;
    var adminBaseUrl = adminAppOptions.BaseUrl.TrimEnd('/');

    app.MapGet("/admin", (HttpContext httpContext) =>
    {
        var targetUrl = $"{adminBaseUrl}/admin{httpContext.Request.QueryString}";
        return Results.Redirect(targetUrl);
    });
    app.MapGet("/admin/{**path}", (HttpContext httpContext, string? path) =>
    {
        var pathSuffix = string.IsNullOrWhiteSpace(path) ? string.Empty : $"/{path}";
        var targetUrl = $"{adminBaseUrl}/admin{pathSuffix}{httpContext.Request.QueryString}";
        return Results.Redirect(targetUrl);
    });

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapAuthEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;

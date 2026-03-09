using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Features.Admin;
using Microsoft.EntityFrameworkCore;
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

    builder.Services.AddDbContext<KonqvistDbContext>(options =>
        options.UseSqlite(connectionString));
    builder.Services
        .AddOptions<AdminAppOptions>()
        .Bind(builder.Configuration.GetSection(AdminAppOptions.SectionName));
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

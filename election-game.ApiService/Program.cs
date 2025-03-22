using election_game.Data;
using election_game.Data.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin();
        policyBuilder.AllowAnyHeader();
        policyBuilder.AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(config =>
    {
        config.SwaggerEndpoint("/openapi/v1.json", "Election API");
    });
}

app.UseCors();

app.MapGet("/mapdata", MapDataHelper.GetMapData);
app.MapGet("/teams", MapDataHelper.GetTeamsData);
app.MapPut("/mapdata/district/{districtName}/owner", UpdateDistrictOwner);

async Task UpdateDistrictOwner(string districtName, TeamData teamData)
{
    throw new NotImplementedException();
}

app.MapDefaultEndpoints();

app.Run();
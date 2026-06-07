using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Engine;
using Nexus.Fms.Core.Scoring;
using Nexus.Fms.Core.Services;
using Nexus.Fms.Infrastructure;
using Nexus.Fms.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurable engines (FR-11/FR-12 threshold bands, FR-04/FR-16 screening options).
builder.Services.Configure<ThresholdBands>(builder.Configuration.GetSection("ThresholdBands"));
builder.Services.Configure<ScreeningOptions>(builder.Configuration.GetSection("Screening"));

builder.Services.AddSingleton<RuleEngine>();
builder.Services.AddScoped<ScoringEngine>();
builder.Services.AddScoped<IScreeningService, ScreeningService>();

builder.Services.AddFmsInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Create the schema and seed the initial rule set in Development (§5).
    // NOTE: for staging/production, replace EnsureCreated with EF Core migrations
    // (dotnet ef migrations add Initial) so schema changes are versioned and auditable (FR-26).
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FmsDbContext>();
    await db.Database.EnsureCreatedAsync();
    // Seed mode is configurable (Seeding:Mode). Spec default is Shadow (tune before go-live);
    // docker-compose sets it to Live so the screening pipeline enforces verdicts out-of-the-box.
    var seedMode = builder.Configuration.GetValue("Seeding:Mode", RuleMode.Shadow);
    await RuleSeeder.SeedAsync(db, seedMode);
    await ListSeeder.SeedAsync(db);
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

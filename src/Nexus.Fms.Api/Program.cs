using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Nexus.Fms.Api.Security;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Domain;
using Nexus.Fms.Core.Engine;
using Nexus.Fms.Core.Scoring;
using Nexus.Fms.Core.Services;
using Nexus.Fms.Infrastructure;
using Nexus.Fms.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Guard: in non-Development environments JWT and Screening keys must be explicitly configured.
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]))
        throw new InvalidOperationException(
            "Jwt:Key must be configured via environment variable or secrets manager. " +
            "Set FMS_JWT__Key (or Jwt:Key) before starting the application.");

    if (string.IsNullOrWhiteSpace(builder.Configuration["Screening:ApiKey"]))
        throw new InvalidOperationException(
            "Screening:ApiKey must be configured. " +
            "Set FMS_SCREENING__APIKEY (or Screening:ApiKey) before starting the application.");
}

// ── JSON / Controllers ─────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ── Swagger with JWT support ───────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Nexus FMS API", Version = "v1" });

    // Allow JWT bearer tokens to be entered in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer token. Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Use OpenApiSecuritySchemeReference (existing in provided signatures) instead of
    // trying to use a non-existent OpenApiReference / Reference property.
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
                        {
                            // Provide required constructor args: referenceId = "Bearer", document = null, referenceType = null
                            new OpenApiSecuritySchemeReference("Bearer", null, null)
                            {
                                Description = "JWT Bearer token. Enter: Bearer {token}",
                            },
                            Array.Empty<string>()
                        }
    });
});

// ── Authentication — JWT bearer (M6-1) ────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"];
if (!string.IsNullOrWhiteSpace(jwtKey))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
}
else
{
    // Development: add a no-op auth scheme so [Authorize] attributes don't crash.
    builder.Services.AddAuthentication().AddJwtBearer();
}

builder.Services.AddAuthorization();

// ── Domain engines (configurable, FR-11/FR-12/FR-04) ──────────────────────────
builder.Services.Configure<ThresholdBands>(builder.Configuration.GetSection("ThresholdBands"));
builder.Services.Configure<ScreeningOptions>(builder.Configuration.GetSection("Screening"));

builder.Services.AddSingleton<RuleEngine>();
builder.Services.AddScoped<ScoringEngine>();
builder.Services.AddScoped<IScreeningService, ScreeningService>();

// ── Infrastructure (DB, NIBSS, repositories, jobs) ───────────────�
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Services;
using Nexus.Fms.Infrastructure.Fraud;
using Nexus.Fms.Infrastructure.Jobs;
using Nexus.Fms.Infrastructure.Nibss;
using Nexus.Fms.Infrastructure.Persistence;

namespace Nexus.Fms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFmsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // ── Database ───────────────────────────────────────────────────────────
        services.AddDbContext<FmsDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Fms")));

        // ── Repositories ───────────────────────────────────────────────────────
        services.AddScoped<IRuleRepository, RuleRepository>();
        services.AddScoped<IListRepository, ListRepository>();
        services.AddScoped<IAlertStore, AlertStore>();
        services.AddScoped<ICaseRepository, CaseRepository>();

        // ── Core services ──────────────────────────────────────────────────────
        services.AddScoped<ICaseManagementService, CaseManagementService>();
        services.AddScoped<ICaseSideEffectsHandler, StubCaseSideEffectsHandler>();

        // ── NIBSS integration (Dependencies §7a) ───────────────────────────────
        var nibssBaseUrl = config["Nibss:BaseUrl"];
        if (string.IsNullOrWhiteSpace(nibssBaseUrl))
        {
            services.AddSingleton<INibssFraudBureauClient, StubNibssFraudBureauClient>();
        }
        else
        {
            var timeoutSeconds = config.GetValue("Nibss:TimeoutSeconds", 5);
            services.AddHttpClient<INibssFraudBureauClient, NibssFraudBureauClient>(c =>
                {
                    c.BaseAddress = new Uri(nibssBaseUrl);
                    c.Timeout     = TimeSpan.FromSeconds(timeoutSeconds); // NFR-08
                    var apiKey = confi
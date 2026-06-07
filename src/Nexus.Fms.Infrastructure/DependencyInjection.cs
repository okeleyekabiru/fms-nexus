using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Core.Services;
using Nexus.Fms.Infrastructure.Fraud;
using Nexus.Fms.Infrastructure.Jobs;
using Nexus.Fms.Infrastructure.Nibss;
using Nexus.Fms.Infrastructure.Notifications;
using Nexus.Fms.Infrastructure.Persistence;
using Nexus.Fms.Infrastructure.Reporting;

namespace Nexus.Fms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFmsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // In-memory cache (CachedRuleRepository, NFR-03)
        services.AddMemoryCache();

        // Database
        services.AddDbContext<FmsDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Fms")));

        // Repositories
        services.AddScoped<RuleRepository>(); // raw DB repository, wrapped by cache decorator
        services.AddScoped<IRuleRepository>(sp =>
            new CachedRuleRepository(
                sp.GetRequiredService<RuleRepository>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachedRuleRepository>>()));
        services.AddScoped<IListRepository, ListRepository>();
        services.AddScoped<IAlertStore, AlertStore>();
        services.AddScoped<ICaseRepository, CaseRepository>();
        services.AddScoped<IAsyncEvaluationQueue, AsyncEvaluationQueue>();
        services.AddScoped<IAuditLogger, AuditLogRepository>();
        services.AddScoped<IReportingService, ReportingService>();

        // Core services
        services.AddScoped<ICaseManagementService, CaseManagementService>();
        services.AddScoped<ICaseSideEffectsHandler, CaseSideEffectsHandler>();

        // Notifications
        services.AddScoped<IFraudNotificationService, LoggingFraudNotificationService>();

        // NIBSS integration (Dependencies 7a)
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
                    var apiKey = config["Nibss:ApiKey"];
                    if (!string.IsNullOrWhiteSpace(apiKey))
                        c.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", apiKey);
                })
                .AddPolicyHandler(RetryPolicy())
                .AddPolicyHandler(CircuitBreakerPolicy());
        }

        // Background jobs
        services.AddHostedService<CaseEscalationJob>();
        services.AddHostedService<AsyncEvaluationJob>();
        services.AddHostedService<RetentionJob>();

        return services;
    }

    // 3 retries with exponential backoff (NFR-08).
    private static IAsyncPolicy<HttpResponseMessage> RetryPolicy() =>
        HttpPolicyExtensions.HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt =>
                TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)));

    // Break for 30s after 5 consecutive failures (NFR-08).
    private static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy() =>
        HttpPolicyExtensions.HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}

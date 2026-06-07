using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Nexus.Fms.Core.Abstractions;
using Nexus.Fms.Infrastructure.Nibss;
using Nexus.Fms.Infrastructure.Persistence;

namespace Nexus.Fms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFmsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<FmsDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Fms")));

        services.AddScoped<IRuleRepository, RuleRepository>();
        services.AddScoped<IListRepository, ListRepository>();
        services.AddScoped<IAlertStore, AlertStore>();

        // NIBSS integration. Use the offline stub unless a base URL is configured (Dependencies §7a).
        var nibssBaseUrl = config["Nibss:BaseUrl"];
        if (string.IsNullOrWhiteSpace(nibssBaseUrl))
        {
            services.AddSingleton<INibssFraudBureauClient, StubNibssFraudBureauClient>();
        }
        else
        {
            services.AddHttpClient<INibssFraudBureauClient, NibssFraudBureauClient>(c =>
                {
                    c.BaseAddress = new Uri(nibssBaseUrl);
                    c.Timeout = TimeSpan.FromSeconds(Convert.ToUInt64(config["Nibss:TimeoutSeconds"])); // NFR-08: 5s timeout
                    var apiKey = config["Nibss:ApiKey"];
                    if (!string.IsNullOrWhiteSpace(apiKey))
                        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                })
                .AddPolicyHandler(RetryPolicy())        // NFR-08: 3 retries
                .AddPolicyHandler(CircuitBreakerPolicy()); // NFR-08: circuit breaker
        }

        return services;
    }

    // 3 retries with exponential backoff (NFR-08).
    private static IAsyncPolicy<HttpResponseMessage> RetryPolicy() =>
        HttpPolicyExtensions.HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)));

    // Break for 30s after 5 consecutive failures.
    private static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy() =>
        HttpPolicyExtensions.HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}

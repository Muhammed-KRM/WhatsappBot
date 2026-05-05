using CafeBot.Business.Infrastructure.AI;
using CafeBot.Business.Infrastructure.WhatsApp;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Interfaces;
using CafeBot.Business.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace CafeBot.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Standart Memory Cache (Performans ve UI önbelleği için)
        services.AddMemoryCache();

        // Http Context & User Service (Multi-Tenant)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Business Services (Managers)
        services.AddScoped<IConfigService, ConfigManager>();
        services.AddScoped<IWhatsAppService, WhatsAppManager>();
        services.AddScoped<IMessageParserService, MessageParserManager>();
        services.AddScoped<ISchedulerService, SchedulerManager>();
        services.AddScoped<INotificationService, NotificationService>();

        // Loglama servisi (OZELDERS pattern - veritabanına yazar)
        services.AddScoped<ILogService, LogManager>();

        // HttpClient for Evolution API with Polly: retry 5x with exponential backoff (2s, 4s, 8s, 16s, 32s)
        services.AddHttpClient(nameof(EvolutionApiClient), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(300); // 180 → 300 saniye
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetTimeoutPolicy());

        // Register EvolutionApiClient with configuration
        services.AddScoped<EvolutionApiClient>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(EvolutionApiClient));

            var baseUrl = configuration["EvolutionApi:BaseUrl"]
                ?? throw new InvalidOperationException("EvolutionApi:BaseUrl is not configured");
            var apiKey = configuration["EvolutionApi:ApiKey"]
                ?? throw new InvalidOperationException("EvolutionApi:ApiKey is not configured");

            return new EvolutionApiClient(httpClient, baseUrl, apiKey);
        });

        // HttpClient for Gemini API with Polly: retry 3x with exponential backoff (1s, 2s, 4s)
        services.AddHttpClient(nameof(GeminiClient), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            
            // Configure for Docker/hosting provider compatibility
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
        {
            // Configure DNS and connection settings for Docker environments
            UseCookies = false,
            UseDefaultCredentials = false,
            // Disable automatic decompression to avoid potential issues
            AutomaticDecompression = System.Net.DecompressionMethods.None
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetTimeoutPolicy());

        // Register GeminiClient with configuration
        services.AddScoped<GeminiClient>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(GeminiClient));

            var baseUrl = configuration["Gemini:BaseUrl"]
                ?? "https://generativelanguage.googleapis.com";
            var apiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Gemini:ApiKey is not configured");
            var model = configuration["Gemini:Model"]
                ?? "gemini-2.5-flash"; // Updated to working model

            return new GeminiClient(httpClient, baseUrl, apiKey, model);
        });

        return services;
    }

    /// <summary>
    /// Retry policy: 5 attempts with exponential backoff (2s, 4s, 8s, 16s, 32s).
    /// Retries on transient HTTP errors (5xx, network failures) and 429 Too Many Requests.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 5, // 3 → 5 retry
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2, 4, 8, 16, 32
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"[EvolutionApiClient] Retry {retryCount} after {timespan.TotalSeconds}s");
                });
    }

    /// <summary>
    /// Timeout policy: 60 seconds per individual attempt (120 → 60 saniye).
    /// Evolution API bazen yavaş yanıt veriyor, daha kısa timeout ile hızlı fail-over
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(60));
    }
}

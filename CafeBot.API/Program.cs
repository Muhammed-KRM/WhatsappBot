using CafeBot.API.Hubs;
using CafeBot.API.Middleware;
using CafeBot.Business;
using CafeBot.Data;
using CafeBot.Data.Context;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// === 1. VERİTABANI (Data Layer) ===
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection yapılandırması eksik.");

builder.Services.AddDataLayer(connectionString);

// === 2. İŞ KATMANI (Business Layer) ===
builder.Services.AddBusinessServices(builder.Configuration);

// === 3. CONTROLLERS ===
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CafeBot API", Version = "v1" });
});

// === 4. SIGNALR ===
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// === 5. CORS ===
builder.Services.AddCors(options =>
{
    // Development: her yerden izin ver (Blazor dev server için)
    options.AddPolicy("AllowAll", b => b
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());

    // Production: sadece izin verilen origin'ler
    options.AddPolicy("Production", b => b
        .WithOrigins(
            builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5001", "https://localhost:5001"])
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
});

// === 6. HEALTH CHECKS ===
var evolutionApiUrl = builder.Configuration["EvolutionApi:BaseUrl"]
    ?? "http://localhost:8080";

builder.Services.AddHttpClient("HealthCheck");

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")
    .AddCheck("evolution-api", new EvolutionApiHealthCheck(evolutionApiUrl));

// === UYGULAMA OLUŞTUR ===
var app = builder.Build();

// === MİDDLEWARE PİPELINE ===

// 1. Global exception handling (en dışta olmalı)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. Request/Response logging (OZELDERS pattern)
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// 2. Swagger (her ortamda açık - API geliştirme kolaylığı için)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CafeBot API v1");
    c.RoutePrefix = "swagger";
});

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", context =>
    {
        context.Response.Redirect("/swagger");
        return Task.CompletedTask;
    });
}

// 3. CORS
app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "Production");

// 4. Auth (şimdilik placeholder - ileride eklenebilir)
app.UseAuthentication();
app.UseAuthorization();

// 5. Controllers
app.MapControllers();

// 6. SignalR Hub
app.MapHub<ActivityHub>("/hubs/activity");

// 7. Health Checks
app.MapHealthChecks("/health");

// === VERİTABANI MİGRASYON ===
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await context.Database.MigrateAsync();
        logger.LogInformation("Veritabanı migration başarılı.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Veritabanı migration sırasında hata oluştu.");
        throw;
    }
}

app.Run();

public partial class Program { }

// Evolution API health check implementasyonu
internal sealed class EvolutionApiHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly string _baseUrl;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public EvolutionApiHealthCheck(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(_baseUrl, cancellationToken);
            return response.IsSuccessStatusCode
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Evolution API erişilebilir.")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded($"Evolution API HTTP {(int)response.StatusCode} döndürdü.");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Evolution API erişilemiyor.", ex);
        }
    }
}

using CafeBot.API.Hubs;
using CafeBot.API.Middleware;
using CafeBot.Business;
using CafeBot.Data;
using CafeBot.Data.Context;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CafeBot.Data.Entities;

var builder = WebApplication.CreateBuilder(args);

// UTF-8 encoding ayarları
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Console.OutputEncoding = Encoding.UTF8;

// === 1. VERİTABANI (Data Layer) ===
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection yapılandırması eksik.");

builder.Services.AddDataLayer(connectionString);

// === 1.1 IDENTITY & JWT (Authentication) ===
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "CafeBotSuperSecretKey_NeedsToBeLongEnoughForHS256!";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "CafeBotApi";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "CafeBotUsers";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

// === 2. İŞ KATMANI (Business Layer) ===
builder.Services.AddBusinessServices(builder.Configuration);

// === 3. CONTROLLERS ===
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // UTF-8 encoding for JSON responses
        options.SuppressConsumesConstraintForFormFileParameters = true;
        options.SuppressInferBindingSourcesForParameters = true;
        options.SuppressModelStateInvalidFilter = true;
    });

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CafeBot API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\""
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
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

// 4.1. API Key Required Middleware (after auth)
app.UseMiddleware<ApiKeyRequiredMiddleware>();

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

        var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Admin"));
            logger.LogInformation("Admin rolü oluşturuldu.");
        }

        // Sabit Admin hesabı oluşturma
        var adminEmail = app.Configuration["AdminSettings:Email"];
        var adminPassword = app.Configuration["AdminSettings:Password"];
        if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<CafeBot.Data.Entities.AppUser>>();
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin == null)
            {
                var adminUser = new CafeBot.Data.Entities.AppUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    CompanyName = "CafeBot Admin"
                };
                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Sabit Admin hesabı oluşturuldu: {Email}", adminEmail);
                }
                else
                {
                    logger.LogWarning("Admin hesabı oluşturulamadı: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
            {
                await userManager.AddToRoleAsync(existingAdmin, "Admin");
                logger.LogInformation("Mevcut kullanıcıya Admin rolü verildi: {Email}", adminEmail);
            }
        }
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

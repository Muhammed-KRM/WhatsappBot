using CafeBot.Business;
using CafeBot.Data;
using CafeBot.Worker.Services;
using Microsoft.AspNetCore.SignalR.Client;

var builder = Host.CreateApplicationBuilder(args);

// Logging konfigürasyonu
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Bağlantı dizesi
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=cafebot.db";

// Data Layer kaydı
builder.Services.AddDataLayer(connectionString);

// Business Services kaydı
builder.Services.AddBusinessServices(builder.Configuration);

// SignalR Client - API'deki ActivityHub'a bağlanır
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";
var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/activity";

builder.Services.AddSingleton<HubConnection>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<HubConnection>>();

    var connection = new HubConnectionBuilder()
        .WithUrl(hubUrl)
        .WithAutomaticReconnect()
        .Build();

    connection.Reconnecting += error =>
    {
        logger.LogWarning("SignalR bağlantısı yeniden kuruluyor... Hata: {Error}", error?.Message);
        return Task.CompletedTask;
    };

    connection.Reconnected += connectionId =>
    {
        logger.LogInformation("SignalR bağlantısı yeniden kuruldu. ConnectionId: {ConnectionId}", connectionId);
        return Task.CompletedTask;
    };

    connection.Closed += error =>
    {
        logger.LogWarning("SignalR bağlantısı kapandı. Hata: {Error}", error?.Message);
        return Task.CompletedTask;
    };

    return connection;
});

// SignalR bağlantısını başlatan hosted service
builder.Services.AddHostedService<SignalRConnectionService>();

// MessageProcessorService - Scoped (her mesaj işleme için yeni instance)
builder.Services.AddScoped<MessageProcessorService>();

// WhatsAppListenerService - BackgroundService
builder.Services.AddHostedService<WhatsAppListenerService>();

var host = builder.Build();

// Veritabanı migration işlemlerini API projesi (CafeBot.API) üstlenmektedir.
// Worker projesinde veritabanı oluşturma işlemi yapılmayacaktır (EnsureCreated/Migrate çakışmasını önlemek için).

host.Run();

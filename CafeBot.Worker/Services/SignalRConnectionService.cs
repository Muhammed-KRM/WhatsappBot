using Microsoft.AspNetCore.SignalR.Client;

namespace CafeBot.Worker.Services;

/// <summary>
/// Uygulama başladığında SignalR HubConnection'ı başlatan ve kapanışta durduran hosted service.
/// </summary>
public class SignalRConnectionService : IHostedService
{
    private readonly HubConnection _hubConnection;
    private readonly ILogger<SignalRConnectionService> _logger;

    public SignalRConnectionService(HubConnection hubConnection, ILogger<SignalRConnectionService> logger)
    {
        _hubConnection = hubConnection;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _hubConnection.StartAsync(cancellationToken);
            _logger.LogInformation("SignalR bağlantısı kuruldu. ConnectionId: {ConnectionId}", _hubConnection.ConnectionId);
        }
        catch (Exception ex)
        {
            // API henüz hazır olmayabilir; hata loglanır ama uygulama çökmez.
            // WithAutomaticReconnect() sayesinde bağlantı otomatik yeniden kurulacak.
            _logger.LogWarning(ex, "SignalR bağlantısı başlatılamadı. Otomatik yeniden bağlanma aktif.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _hubConnection.StopAsync(cancellationToken);
            _logger.LogInformation("SignalR bağlantısı kapatıldı.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR bağlantısı kapatılırken hata oluştu");
        }
    }
}

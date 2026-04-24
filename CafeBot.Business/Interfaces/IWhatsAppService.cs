using CafeBot.Business.DTOs;
using CafeBot.Data.Enums;

namespace CafeBot.Business.Interfaces;

public interface IWhatsAppService
{
    Task<QRCodeDto> InitializeSessionAsync();
    Task<ConnectionStatus> GetConnectionStatusAsync();
    Task<List<GroupDto>> GetGroupsAsync();
    Task<bool> SendMessageAsync(string groupId, string message);
    /// <summary>
    /// Belirli bir gruba mesaj gönderir.
    /// </summary>
    Task<bool> SendMessageAsync(string instanceName, string groupId, string message, string? traceId = null); // Webhook için overload
    Task<List<string>> GetAllInstancesAsync(); // Instance listesi için
    Task DisconnectAsync();
}

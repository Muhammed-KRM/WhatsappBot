using CafeBot.Business.DTOs;
using CafeBot.Data.Enums;

namespace CafeBot.Business.Interfaces;

public interface IWhatsAppService
{
    Task<QRCodeDto> InitializeSessionAsync(string? traceId = null);
    Task<ConnectionStatus> GetConnectionStatusAsync(string? traceId = null);
    Task<List<GroupDto>> GetGroupsAsync(bool forceRefresh = false);
    Task<bool> SendMessageAsync(string groupId, string message);
    /// <summary>
    /// Belirli bir gruba mesaj gönderir.
    /// </summary>
    Task<bool> SendMessageAsync(string instanceName, string groupId, string message, string? traceId = null); // Webhook için overload
    Task<List<string>> GetAllInstancesAsync(); // Instance listesi için
    Task DisconnectAsync(string? traceId = null);
}

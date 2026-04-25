using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using CafeBot.Business.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CafeBot.Business.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IConfiguration _configuration;
    private static readonly ConcurrentDictionary<string, DateTime> _lastNotifications = new();
    private const int FloodProtectionMinutes = 60; // 1 saatte en fazla 1 bildirim

    public NotificationService(ILogger<NotificationService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendConnectionLostAlertAsync(string sessionName, DateTime disconnectTime, string userEmail)
    {
        // 1. Flood Koruması (Kiracı Bazlı)
        if (_lastNotifications.TryGetValue(sessionName, out var lastSent))
        {
            if ((DateTime.UtcNow - lastSent).TotalMinutes < FloodProtectionMinutes)
            {
                _logger.LogInformation("{SessionName} için yakın zamanda bildirim gönderildi. Flood koruması devrede.", sessionName);
                return;
            }
        }

        // 2. Her halükarda konsola devasa bir uyarı bas (Docker loglarından görülmesi için)
        _logger.LogCritical("\n=======================================================\n" +
                            "⚠️ DİKKAT! CAFEBOT WHATSAPP BAĞLANTISI KOPTU! ⚠️\n" +
                            $"Oturum: {sessionName}\n" +
                            $"Kullanıcı: {userEmail}\n" +
                            $"Kopma Zamanı: {disconnectTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n" +
                            "Lütfen panele girip QR kodunu tekrar okutunuz.\n" +
                            "=======================================================\n");

        _lastNotifications[sessionName] = DateTime.UtcNow;

        // 3. E-Posta Gönderimi (Eğer .env/appsettings içinde SMTP ayarları varsa)
        var smtpHost = _configuration["Smtp:Host"];
        if (string.IsNullOrEmpty(smtpHost))
        {
            _logger.LogWarning("SMTP ayarları bulunamadığı için e-posta bildirimi gönderilemedi. Sadece loglara yazıldı.");
            return;
        }

        try
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_configuration["Smtp:Username"] ?? "noreply@cafebot.local"),
                Subject = "⚠️ ACİL: CafeBot WhatsApp Bağlantısı Koptu",
                Body = $"Merhaba,\n\nCafeBot sisteminin WhatsApp bağlantısı koptu.\nMesajlara şu anda cevap verilemiyor.\n\nLütfen en kısa sürede yönetim paneline girip QR kodunu tekrar okutunuz.\nKopma zamanı: {disconnectTime.ToLocalTime()}",
                IsBodyHtml = false,
            };

            var toAddress = userEmail;
            if (string.IsNullOrEmpty(toAddress)) return;

            mailMessage.To.Add(toAddress);

            using var smtpClient = new SmtpClient(smtpHost)
            {
                Port = int.Parse(_configuration["Smtp:Port"] ?? "587"),
                Credentials = new NetworkCredential(_configuration["Smtp:Username"], _configuration["Smtp:Password"]),
                EnableSsl = true,
            };

            await smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation("Bağlantı kopukluğu e-postası {Email} adresine başarıyla gönderildi.", toAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı kopukluğu e-postası gönderilirken bir hata oluştu.");
        }
    }
}

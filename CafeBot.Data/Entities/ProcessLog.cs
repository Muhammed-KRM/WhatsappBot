namespace CafeBot.Data.Entities;

/// <summary>
/// Mesaj işleme sürecinin adım adım kaydını tutan entity.
/// Her mesaj için TraceId ile gruplandırılmış adımlar (StepOrder) saklanır.
/// VS breakpoint mantığıyla: her fonksiyonda ne girdi, ne çıktı loglanır.
/// </summary>
public class ProcessLog : ITenantEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>Aynı mesajın tüm adımlarını gruplar (genelde MessageId)</summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>Adım sırası: 1, 2, 3, ... (timeline için)</summary>
    public int StepOrder { get; set; }

    /// <summary>Adım adı: "WebhookReceived", "ConfigLoaded", "ShiftDetected" vb.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Fonksiyon adı: "WebhookController.ProcessSingleMessageAsync"</summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>Fonksiyona giren veri (JSON formatında)</summary>
    public string? InputData { get; set; }

    /// <summary>Fonksiyondan çıkan veri (JSON formatında)</summary>
    public string? OutputData { get; set; }

    /// <summary>Adım durumu: "OK", "Error", "Skip"</summary>
    public string Status { get; set; } = "OK";

    /// <summary>Hata varsa detay mesajı</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Bu adımın süresi (milisaniye)</summary>
    public int DurationMs { get; set; }

    /// <summary>Kayıt zamanı</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

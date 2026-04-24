using System.Text.Json.Serialization;

namespace CafeBot.Business.Infrastructure.WhatsApp;

// ===== SESSION CREATE =====

public record CreateSessionResponse
{
    [JsonPropertyName("instance")]
    public CreateSessionInstance? InstanceData { get; set; }

    [JsonPropertyName("qrcode")]
    public CreateSessionQrCode? QrCodeData { get; set; }

    // Basit durum takibi için (JSON serileştirmeden hariç - "instance" ile çakışmasın diye)
    [JsonIgnore]
    public string? StatusInfo { get; set; }
    [JsonIgnore]
    public string? InstanceName { get; set; }
}

public record CreateSessionInstance
{
    [JsonPropertyName("instanceName")]
    public string? InstanceName { get; set; }

    [JsonPropertyName("instanceId")]
    public string? InstanceId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("connectionStatus")]
    public string? ConnectionStatus { get; set; }
}

public record CreateSessionQrCode
{
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("base64")]
    public string? Base64 { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("qrcode")]
    public string? QrCode { get; set; }
}

// ===== QR CODE =====

public record QRCodeResponse
{
    [JsonPropertyName("base64")]
    public string? Base64 { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }
}

// ===== SESSION STATUS =====

public record SessionStatusResponse
{
    [JsonPropertyName("instance")]
    public SessionStatusInstance? InstanceData { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

public record SessionStatusInstance
{
    [JsonPropertyName("instanceName")]
    public string? InstanceName { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("connectionStatus")]
    public string? ConnectionStatus { get; set; }
}

// ===== GROUPS =====

public record GroupResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("participants")]
    public List<GroupParticipant>? Participants { get; set; }
}

public record GroupParticipant
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("admin")]
    public string? Admin { get; set; }
}

// ===== SEND MESSAGE =====

public record SendMessageResponse
{
    [JsonPropertyName("key")]
    public MessageKey? Key { get; set; }

    /// <summary>
    /// Evolution API v1.8.2 status'ü integer (1=pending, 2=sent) olarak döndürür.
    /// Bazı versiyonlarda string olabilir. object? ile her ikisini de destekliyoruz.
    /// </summary>
    [JsonPropertyName("status")]
    public object? Status { get; set; }
}

public record MessageKey
{
    [JsonPropertyName("remoteJid")]
    public string? RemoteJid { get; set; }

    [JsonPropertyName("fromMe")]
    public bool? FromMe { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

// ===== REQUEST MODELS =====

public record CreateSessionRequest
{
    [JsonPropertyName("instanceName")]
    public string InstanceName { get; set; } = string.Empty;

    [JsonPropertyName("qrcode")]
    public bool Qrcode { get; set; } = true;

    [JsonPropertyName("integration")]
    public string Integration { get; set; } = "WHATSAPP-BAILEYS";
}

public record SendTextMessageRequest
{
    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

// ===== MESSAGES =====

public record EvolutionMessage
{
    [JsonPropertyName("key")]
    public EvolutionMessageKey? MessageKey { get; set; }

    [JsonPropertyName("message")]
    public EvolutionMessageContent? Message { get; set; }

    [JsonPropertyName("messageTimestamp")]
    public long? MessageTimestamp { get; set; }

    [JsonPropertyName("pushName")]
    public string? PushName { get; set; }

    public string? Id { get; set; }
}

public record EvolutionMessageKey
{
    [JsonPropertyName("remoteJid")]
    public string? RemoteJid { get; set; }

    [JsonPropertyName("fromMe")]
    public bool? FromMe { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public record EvolutionMessageContent
{
    [JsonPropertyName("conversation")]
    public string? Conversation { get; set; }

    [JsonPropertyName("extendedTextMessage")]
    public EvolutionExtendedTextMessage? ExtendedTextMessage { get; set; }
}

public record EvolutionExtendedTextMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

using System.Text.RegularExpressions;
using CafeBot.Business.DTOs;
using CafeBot.Business.Infrastructure.AI;
using CafeBot.Business.Interfaces;
using Microsoft.Extensions.Logging;

namespace CafeBot.Business.Services;

public class MessageParserManager : IMessageParserService
{
    private readonly GeminiClient _geminiClient;
    private readonly ILogService _logService;
    private readonly ILogger<MessageParserManager> _logger;

    private static readonly string[] ShiftKeywords =
    [
        "saat", "saatte", "saatler", "saatlerde",
        "kişi", "kişiye", "kişilik",
        "lazım", "gerek", "ihtiyaç",
        "vardiya", "shift",
        "çalışan", "eleman",
        "arıyorum", "aranıyor"
    ];

    // Regex patterns for fallback parsing
    // "16:00 için 3 kişi" veya "saat 16 için 3 kişi" veya "16.00 için 3 kişi"
    private static readonly Regex SlotPattern = new(
        @"(\d{1,2})[:\.]?(?:00)?\s*(?:için|de|da|te|ta|'de|'da|'te|'ta|-)?\s*(\d{1,2})\s*(?:kişi|kişiye|kişilik|adam|eleman|çalışan)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Alternatif: "3 kişi saat 16" veya "3 kişi 16:00"
    private static readonly Regex SlotPatternAlt = new(
        @"(\d{1,2})\s*(?:kişi|kişiye|kişilik|adam|eleman|çalışan)\s*(?:saat\s*)?(\d{1,2})[:\.]?(?:00)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MessageParserManager(GeminiClient geminiClient, ILogService logService, ILogger<MessageParserManager> logger)
    {
        _geminiClient = geminiClient ?? throw new ArgumentNullException(nameof(geminiClient));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsShiftMessage(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText)) return false;
        var lowerText = messageText.ToLowerInvariant();
        return ShiftKeywords.Any(keyword => lowerText.Contains(keyword));
    }

    public async Task<List<ShiftSlotDto>?> ParseShiftMessageAsync(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText)) return null;

        // 1. Önce Gemini ile dene
        List<ShiftSlotDto>? geminiResult = null;
        try
        {
            var parseResult = await _geminiClient.ParseShiftMessageAsync(messageText);

            if (parseResult != null && parseResult.IsShiftMessage && parseResult.Slots.Count > 0)
            {
                geminiResult = parseResult.Slots
                    .Where(slot => slot.Hour >= 0 && slot.Hour <= 23 && slot.PersonCount > 0)
                    .Select(slot => new ShiftSlotDto(slot.Hour, slot.PersonCount))
                    .ToList();

                if (geminiResult.Count > 0)
                {
                    _logger.LogInformation("Gemini ile {Count} slot ayrıştırıldı.", geminiResult.Count);
                    return geminiResult;
                }
            }

            _logger.LogDebug("Gemini sonuç döndürmedi, regex fallback deneniyor.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini parse hatası, regex fallback deneniyor. Mesaj: {Text}",
                messageText.Length > 100 ? messageText[..100] + "..." : messageText);
            await _logService.LogFunctionErrorAsync("GEMINI_PARSE_ERROR", ex, new { messageText });
        }

        // 2. Regex fallback parser
        try
        {
            var regexResult = ParseWithRegex(messageText);
            if (regexResult != null && regexResult.Count > 0)
            {
                _logger.LogInformation("Regex fallback ile {Count} slot ayrıştırıldı.", regexResult.Count);
                return regexResult;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Regex fallback da başarısız oldu.");
        }

        _logger.LogWarning("Mesaj ne Gemini ne de regex ile ayrıştırılamadı. Text: {Text}", messageText);
        return null;
    }

    /// <summary>
    /// Regex-based fallback parser.
    /// Desteklenen formatlar:
    ///   "16:00 için 3 kişi"
    ///   "saat 16 için 3 kişi"
    ///   "16.00 için 3 kişi"
    ///   "3 kişi saat 16"
    /// </summary>
    private List<ShiftSlotDto>? ParseWithRegex(string text)
    {
        var slots = new List<ShiftSlotDto>();

        // Pattern 1: "16:00 için 3 kişi"
        var matches = SlotPattern.Matches(text);
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out var hour) &&
                int.TryParse(match.Groups[2].Value, out var personCount) &&
                hour >= 0 && hour <= 23 && personCount > 0)
            {
                slots.Add(new ShiftSlotDto(hour, personCount));
            }
        }

        // Pattern 2: "3 kişi saat 16" (eğer pattern 1 sonuç vermediyse)
        if (slots.Count == 0)
        {
            var altMatches = SlotPatternAlt.Matches(text);
            foreach (Match match in altMatches)
            {
                if (int.TryParse(match.Groups[1].Value, out var personCount) &&
                    int.TryParse(match.Groups[2].Value, out var hour) &&
                    hour >= 0 && hour <= 23 && personCount > 0)
                {
                    slots.Add(new ShiftSlotDto(hour, personCount));
                }
            }
        }

        // Duplicate saatleri kaldır
        slots = slots
            .GroupBy(s => s.Hour)
            .Select(g => g.First())
            .OrderBy(s => s.Hour)
            .ToList();

        _logger.LogDebug("Regex parser sonucu: {Count} slot bulundu. Slotlar: {Slots}",
            slots.Count,
            string.Join(", ", slots.Select(s => $"{s.Hour}:00={s.PersonCount}kişi")));

        return slots.Count > 0 ? slots : null;
    }
}

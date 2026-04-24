namespace CafeBot.Business.Infrastructure.AI;

/// <summary>
/// Request to Gemini API for content generation
/// </summary>
public record GeminiRequest
{
    public List<GeminiContent> Contents { get; set; } = [];
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

/// <summary>
/// Content part of Gemini request
/// </summary>
public record GeminiContent
{
    public List<GeminiPart> Parts { get; set; } = [];
}

/// <summary>
/// Part of content containing text
/// </summary>
public record GeminiPart
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Generation configuration for Gemini API
/// </summary>
public record GeminiGenerationConfig
{
    public double? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }
}

/// <summary>
/// Response from Gemini API
/// </summary>
public record GeminiResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

/// <summary>
/// Candidate response from Gemini
/// </summary>
public record GeminiCandidate
{
    public GeminiContent? Content { get; set; }
    public string? FinishReason { get; set; }
    public int? Index { get; set; }
}

/// <summary>
/// Usage metadata from Gemini API
/// </summary>
public record GeminiUsageMetadata
{
    public int? PromptTokenCount { get; set; }
    public int? CandidatesTokenCount { get; set; }
    public int? TotalTokenCount { get; set; }
}

/// <summary>
/// Parsed shift message result
/// </summary>
public record ShiftParseResult
{
    public bool IsShiftMessage { get; set; }
    public List<ShiftSlot> Slots { get; set; } = [];
}

/// <summary>
/// Individual shift slot with hour and person count
/// </summary>
public record ShiftSlot
{
    public int Hour { get; set; }
    public int PersonCount { get; set; }
}

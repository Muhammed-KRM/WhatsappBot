using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using Polly;
using Polly.Retry;

namespace CafeBot.Business.Infrastructure.AI;

/// <summary>
/// Client for interacting with Google Gemini API for AI-powered message parsing
/// </summary>
public class GeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly Queue<DateTime> _requestTimestamps;
    private readonly object _lock = new();

    // Retry policy: 3 attempts with exponential backoff (1s, 2s, 4s)
    private static readonly ResiliencePipeline<HttpResponseMessage> RetryPipeline =
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                UseJitter = false,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            })
            .Build();

    // Rate limiting: 15 requests per minute (free tier)
    private const int MaxRequestsPerMinute = 15;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);

    public GeminiClient(HttpClient httpClient, string baseUrl, string apiKey, string model)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = model ?? throw new ArgumentNullException(nameof(model));

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL cannot be empty", nameof(baseUrl));

        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        
        // Set proper User-Agent header to avoid Docker/hosting provider blocks
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
        
        // Set additional headers that might help with hosting provider blocks
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        
        _rateLimiter = new SemaphoreSlim(1, 1);
        _requestTimestamps = new Queue<DateTime>();
    }

    /// <summary>
    /// Generates content using Gemini API with rate limiting
    /// </summary>
    /// <param name="prompt">The prompt text to send to Gemini</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response from Gemini API</returns>
    public async Task<GeminiResponse> GenerateContentAsync(string prompt, string? apiKeyOverride = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        // Apply rate limiting
        await ApplyRateLimitAsync(cancellationToken);

        var request = new GeminiRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Parts =
                    [
                        new GeminiPart { Text = prompt }
                    ]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = 0.1, // Low temperature for more deterministic parsing
                MaxOutputTokens = 500
            }
        };

        var keyToUse = string.IsNullOrWhiteSpace(apiKeyOverride) ? _apiKey : apiKeyOverride;
        var endpoint = $"/v1beta/models/{_model}:generateContent?key={keyToUse}";
        var response = await RetryPipeline.ExecuteAsync(
            async ct => await _httpClient.PostAsJsonAsync(endpoint, request, ct),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to deserialize GeminiResponse");
    }

    /// <summary>
    /// Tests if an API key is valid by making a simple request to Gemini API
    /// </summary>
    /// <param name="apiKey">The API key to test</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if API key is valid and working</returns>
    public async Task<bool> TestApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        try
        {
            // Simple test prompt
            const string testPrompt = "Test message: Hello";
            
            var request = new GeminiRequest
            {
                Contents =
                [
                    new GeminiContent
                    {
                        Parts =
                        [
                            new GeminiPart { Text = testPrompt }
                        ]
                    }
                ],
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.1,
                    MaxOutputTokens = 10 // Minimal response to save quota
                }
            };

            var endpoint = $"/v1beta/models/{_model}:generateContent?key={apiKey}";
            Console.WriteLine($"[GeminiClient] Testing API key with endpoint: {_httpClient.BaseAddress}{endpoint}");
            Console.WriteLine($"[GeminiClient] User-Agent: {_httpClient.DefaultRequestHeaders.UserAgent}");
            
            // Create request message with explicit headers
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            requestMessage.Content = JsonContent.Create(request);
            
            // Ensure proper headers are set for this specific request
            requestMessage.Headers.Add("Accept", "application/json");
            requestMessage.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            
            Console.WriteLine($"[GeminiClient] Response status: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"[GeminiClient] Error response: {errorContent}");
                
                // Check for specific error messages that indicate hosting provider blocks
                if (errorContent.Contains("User location is not supported") || 
                    errorContent.Contains("FAILED_PRECONDITION") ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine("[GeminiClient] Detected hosting provider block. This is likely due to your server's IP being blocked by Google.");
                    Console.WriteLine("[GeminiClient] Common causes: Hetzner, DigitalOcean, or other VPS providers may be blocked.");
                    Console.WriteLine("[GeminiClient] Solutions: 1) Use a different hosting provider, 2) Use a proxy/VPN, 3) Contact Google support");
                }
            }
            
            // If we get a successful response, the API key is valid
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[GeminiClient] HTTP Exception during API key test: {ex.Message}");
            Console.WriteLine("[GeminiClient] This might indicate network connectivity issues or DNS resolution problems in Docker.");
            return false;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Console.WriteLine($"[GeminiClient] Timeout during API key test: {ex.Message}");
            Console.WriteLine("[GeminiClient] The request timed out. This might indicate network issues or server overload.");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GeminiClient] Unexpected exception during API key test: {ex.Message}");
            Console.WriteLine($"[GeminiClient] Exception type: {ex.GetType().Name}");
            return false;
        }
    }

    public const string DefaultShiftParsePrompt = @"Bu WhatsApp mesajından vardiya saatlerini ve kişi sayılarını çıkar.
Mesaj: '{0}'

Yanıtı sadece JSON formatında ver, başka açıklama ekleme:
{{
  ""isShiftMessage"": true/false,
  ""slots"": [
    {{ ""hour"": 18, ""personCount"": 3 }},
    {{ ""hour"": 19, ""personCount"": 5 }}
  ]
}}

Kurallar:
- Eğer mesaj vardiya/saat bilgisi içermiyorsa isShiftMessage: false dön
- Saat değerleri 0-23 arası tam sayı olmalı
- Kişi sayısı pozitif tam sayı olmalı
- Sadece geçerli JSON dön, başka metin ekleme";

    /// <summary>
    /// Parses a WhatsApp message to extract shift information
    /// </summary>
    /// <param name="messageText">The WhatsApp message text</param>
    /// <param name="apiKeyOverride">Optional custom API key for the user</param>
    /// <param name="aiSystemPromptOverride">Optional custom AI prompt</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed shift information or null if parsing fails</returns>
    public async Task<ShiftParseResult?> ParseShiftMessageAsync(string messageText, string? apiKeyOverride = null, string? aiSystemPromptOverride = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return null;

        try
        {
            var promptTemplate = string.IsNullOrWhiteSpace(aiSystemPromptOverride) ? DefaultShiftParsePrompt : aiSystemPromptOverride;
            var prompt = string.Format(promptTemplate, messageText);
            var response = await GenerateContentAsync(prompt, apiKeyOverride, cancellationToken);

            // Extract text from response
            var responseText = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(responseText))
                return null;

            // Clean up response text (remove markdown code blocks if present)
            responseText = CleanJsonResponse(responseText);

            // Parse JSON response
            var parseResult = JsonSerializer.Deserialize<ShiftParseResult>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return parseResult;
        }
        catch (Exception)
        {
            // Log error in production, return null for now
            return null;
        }
    }

    /// <summary>
    /// Applies rate limiting to ensure we don't exceed API limits
    /// </summary>
    private async Task ApplyRateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = now - RateLimitWindow;

            // Remove timestamps outside the current window
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
            {
                _requestTimestamps.Dequeue();
            }

            // If we've hit the limit, calculate wait time
            if (_requestTimestamps.Count >= MaxRequestsPerMinute)
            {
                var oldestRequest = _requestTimestamps.Peek();
                var waitTime = oldestRequest.Add(RateLimitWindow) - now;
                
                if (waitTime > TimeSpan.Zero)
                {
                    // Wait until the oldest request expires asynchronusly
                    await Task.Delay(waitTime, cancellationToken);
                    
                    // Clean up again after waiting
                    now = DateTime.UtcNow;
                    windowStart = now - RateLimitWindow;
                    while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
                    {
                        _requestTimestamps.Dequeue();
                    }
                }
            }

            // Record this request
            _requestTimestamps.Enqueue(now);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    /// <summary>
    /// Cleans JSON response by removing markdown code blocks and extra whitespace
    /// </summary>
    private static string CleanJsonResponse(string responseText)
    {
        // Remove markdown code blocks (```json ... ``` or ``` ... ```)
        responseText = responseText.Trim();
        
        if (responseText.StartsWith("```json"))
        {
            responseText = responseText[7..]; // Remove ```json
        }
        else if (responseText.StartsWith("```"))
        {
            responseText = responseText[3..]; // Remove ```
        }

        if (responseText.EndsWith("```"))
        {
            responseText = responseText[..^3]; // Remove trailing ```
        }

        return responseText.Trim();
    }
}

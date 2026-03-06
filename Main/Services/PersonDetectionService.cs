using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services;

/// <summary>
/// Service that uses vision AI to detect if an image contains live persons
/// before running expensive face detection operations
/// </summary>
public class PersonDetectionService
{
    private readonly ILogger<PersonDetectionService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _serviceUrl;
    private readonly string _modelName;
    private readonly bool _enabled;

    public PersonDetectionService(
        ILogger<PersonDetectionService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serviceUrl = configuration["CaptionService:Url"] ?? "http://localhost:5556";
        _modelName = configuration["CaptionService:ModelName"] ?? "blip2-flan-t5-xl";
        _enabled = configuration.GetValue<bool>("PersonDetection:EnablePreScreening", false);
    }

    public bool IsEnabled => _enabled;

    /// <summary>
    /// Analyzes an image to determine if it contains live persons
    /// </summary>
    /// <param name="imageBytes">Image data as byte array</param>
    /// <returns>True if the image contains live persons, False otherwise</returns>
    public async Task<bool> ContainsLivePersonsAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("[Ollama] Person pre-screening is disabled");
            return true; // Default to true if disabled - process all images
        }

        if (imageBytes == null || imageBytes.Length == 0)
        {
            _logger.LogWarning("[Ollama] Image bytes are null or empty");
            return true; // Default to true
        }

        const int maxRetries = 3;
        const int retryDelayMs = 5000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromMinutes(2);

                // Convert image to base64
                var base64Image = Convert.ToBase64String(imageBytes);
                var imageDataUrl = $"data:image/jpeg;base64,{base64Image}";

                // Create request - ask if image contains live persons
                var request = new
                {
                    model = _modelName,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "text",
                                    text = "Does this image contain one or more live persons (real human beings, not drawings, paintings, sculptures, statues, or photos of photos)? Answer with only 'yes' or 'no'."
                                },
                                new { type = "image_url", image_url = new { url = imageDataUrl } }
                            }
                        }
                    },
                    max_tokens = 200, // Increased to allow full reasoning + answer
                    temperature = 0.1 // Lower temperature for more consistent yes/no answers
                };

                var requestJson = JsonSerializer.Serialize(request);
                _logger.LogDebug($"[Ollama] Checking for live persons using model: {_modelName}");

                var jsonContent = new StringContent(
                    requestJson,
                    Encoding.UTF8,
                    "application/json");

                // Call Ollama API
                var response = await httpClient.PostAsync(
                    $"{_serviceUrl}/v1/chat/completions",
                    jsonContent,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning($"[Ollama] Server returned status code: {response.StatusCode}, Body: {errorContent}");

                    if (attempt < maxRetries)
                    {
                        _logger.LogInformation($"[Ollama] Server does not respond. Retrying (attempt {attempt}/{maxRetries})...");
                        await Task.Delay(retryDelayMs, cancellationToken);
                        continue;
                    }

                    return true; // Default to true on final retry
                }

                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation($"[Ollama PersonDetection] Raw response: {result}");

                var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(result);
                var message = ollamaResponse?.Choices?[0]?.Message;

                // Try content first, then reasoning field (qwen3-vl puts answers in reasoning)
                var answer = message?.Content?.Trim().ToLower();
                _logger.LogInformation($"[Ollama PersonDetection] Content field: '{answer}'");

                if (string.IsNullOrEmpty(answer))
                {
                    answer = message?.Reasoning?.Trim().ToLower();
                    _logger.LogInformation($"[Ollama PersonDetection] Using reasoning field: '{answer?.Substring(0, Math.Min(100, answer?.Length ?? 0))}'");
                }

                if (string.IsNullOrEmpty(answer))
                {
                    _logger.LogWarning($"[Ollama PersonDetection] Both content and reasoning are empty. Defaulting to true");
                    return true; // Default to true when empty response
                }

                _logger.LogInformation($"[Ollama PersonDetection] Final answer for person detection: '{answer}'");

                // Check if the answer contains "yes" (flexible matching)
                bool hasPersons = answer?.Contains("yes") == true || answer?.Contains("да") == true;

                return hasPersons;
            }
            catch (HttpRequestException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning($"[Ollama] Server does not respond. Waiting (attempt {attempt}/{maxRetries})... Error: {ex.Message}");
                    await Task.Delay(retryDelayMs, cancellationToken);
                    continue;
                }

                _logger.LogWarning(ex, "[Ollama] Server does not respond after {MaxRetries} attempts.", maxRetries);
                return true; // Default to true on error
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning($"[Ollama] Request timed out. Retrying (attempt {attempt}/{maxRetries})...");
                    await Task.Delay(retryDelayMs, cancellationToken);
                    continue;
                }

                _logger.LogWarning("[Ollama] Request timed out after {MaxRetries} attempts.", maxRetries);
                return true; // Default to true on timeout
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Ollama] Unexpected error in person detection.");
                return true; // Default to true on error
            }
        }

        return true; // Should not reach here, but default to true for safety
    }

    private class OllamaResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("choices")]
        public Choice[] Choices { get; set; }
    }

    private class Choice
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public Message Message { get; set; }
    }

    private class Message
    {
        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string Content { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reasoning")]
        public string Reasoning { get; set; }
    }
}

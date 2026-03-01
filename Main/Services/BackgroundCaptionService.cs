using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Data;
using System.Text.Json;

namespace Services;

public class BackgroundCaptionService : BackgroundService
{
    private readonly ILogger<BackgroundCaptionService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _captionServiceUrl;
    private readonly string _modelName;
    private readonly string _botUsername;
    private readonly string _botEmail;

    public BackgroundCaptionService(
        ILogger<BackgroundCaptionService> logger,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _captionServiceUrl = configuration["CaptionService:Url"] ?? "http://localhost:5556";
        _modelName = configuration["CaptionService:ModelName"] ?? "blip2-flan-t5-xl";
        _botUsername = $"bot-{_modelName}";
        _botEmail = $"{_botUsername}@svema.ai";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Caption Service is starting.");

        // Wait 30 seconds before starting to allow other services to initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateCaptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing background caption generation.");
            }

            // Wait 10 seconds before checking again
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("Background Caption Service is stopping.");
    }

    private async Task GenerateCaptionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            var botUser = await GetOrCreateBotUserAsync(dbContext, stoppingToken);
            if (botUser == null)
            {
                _logger.LogWarning("Failed to get or create bot user.");
                return;
            }

            var shotsToCaption = await dbContext.Shots
                .Where(s => !dbContext.ShotComments.Any(sc => sc.ShotId == s.ShotId && sc.AuthorId == botUser.UserId))
                .Where(s => s.FullScreen != null || s.Preview != null)
                .OrderByDescending(s => s.ShotId)
                .Take(20)
                .ToListAsync(stoppingToken);

            if (!shotsToCaption.Any())
            {
                _logger.LogDebug("No shots found that need captions.");
                return;
            }

            _logger.LogInformation($"Found {shotsToCaption.Count} shots to caption.");

            int processed = 0;
            int failed = 0;

            foreach (var shot in shotsToCaption)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    var imageBytes = shot.FullScreen ?? shot.Preview;
                    if (imageBytes == null)
                    {
                        _logger.LogWarning($"Shot {shot.ShotId}: No image data available.");
                        failed++;
                        continue;
                    }

                    var caption = await CallCaptionServiceAsync(imageBytes, stoppingToken);
                    if (string.IsNullOrEmpty(caption))
                    {
                        _logger.LogWarning($"Shot {shot.ShotId}: Caption service returned empty caption.");
                        failed++;
                        continue;
                    }

                    // --- Clean up model reasoning/thinking output ---
                    caption = CleanModelReasoning(caption);
                    // --------------------------------------------------------

                    var comment = new ShotComment
                    {
                        AuthorId = botUser.UserId,
                        AuthorUsername = _botUsername,
                        ShotId = shot.ShotId,
                        Timestamp = DateTime.UtcNow,
                        Text = caption
                    };

                    dbContext.ShotComments.Add(comment);

                    // Detect artwork from caption and mark as no_faces to skip face detection
                    if (IsArtwork(caption))
                    {
                        shot.NoFaces = true;
                        _logger.LogInformation($"Shot {shot.ShotId}: Detected as artwork, marked as no_faces to skip face detection.");
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);

                    processed++;
                    _logger.LogInformation($"Shot {shot.ShotId}: Generated caption '{caption}'");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing shot {shot.ShotId}.");
                    failed++;
                }
            }

            _logger.LogInformation($"Caption generation complete. Processed: {processed}, Failed: {failed}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in caption generation batch.");
        }
    }

    private async Task<User> GetOrCreateBotUserAsync(ApplicationDbContext dbContext, CancellationToken stoppingToken)
    {
        var botUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == _botUsername, stoppingToken);

        if (botUser != null)
            return botUser;

        try
        {
            botUser = new User
            {
                Username = _botUsername,
                PasswordHash = "",
                Email = _botEmail
            };

            dbContext.Users.Add(botUser);
            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogInformation($"Created bot user: {_botUsername} with ID {botUser.UserId}");
            return botUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to create bot user: {_botUsername}");
            return null;
        }
    }

    private async Task<string> CallCaptionServiceAsync(byte[] imageBytes, CancellationToken stoppingToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            var base64Image = Convert.ToBase64String(imageBytes);
            var imageDataUrl = $"data:image/jpeg;base64,{base64Image}";

            var request = new
            {
                model = _modelName,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Ты — система описания изображений.\n" +
                                  "ЗАПРЕЩЕНО выводить рассуждения, мысли, планы.\n" +
                                  "ЗАПРЕЩЕНО слова: \"хорошо\", \"мне нужно\", \"я\".\n" +
                                  "Выводи ТОЛЬКО готовое описание изображения одним абзацем."                                
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Опиши изображение по-русски. Кратко и фактически. /no_think" },
                            new { type = "image_url", image_url = new { url = imageDataUrl } }
                        }
                    }
                },
                max_tokens = 300
            };

            var requestJson = JsonSerializer.Serialize(request);
            _logger.LogInformation($"Sending request to Ollama with model: {_modelName}");

            var jsonContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{_captionServiceUrl}/v1/chat/completions", jsonContent, stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(stoppingToken);
                _logger.LogWarning($"Ollama service returned status code: {response.StatusCode}, Body: {errorContent}");
                return null;
            }

            var result = await response.Content.ReadAsStringAsync(stoppingToken);
            _logger.LogInformation($"Ollama raw response: {result?.Substring(0, Math.Min(500, result?.Length ?? 0))}");

            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(result);
            var message = ollamaResponse?.Choices?[0]?.Message;

            // Use content if available, otherwise fallback to reasoning (for models that use OpenAI reasoning format)
            var caption = !string.IsNullOrWhiteSpace(message?.Content)
                ? message.Content.Trim()
                : message?.Reasoning?.Trim();

            _logger.LogInformation($"Parsed caption - Choices count: {ollamaResponse?.Choices?.Length ?? 0}, Content: '{message?.Content}', Reasoning: '{message?.Reasoning?.Substring(0, Math.Min(50, message?.Reasoning?.Length ?? 0))}', Final: '{caption?.Substring(0, Math.Min(100, caption?.Length ?? 0))}'");

            return caption;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Ollama service.");
            return null;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != stoppingToken)
        {
            _logger.LogWarning("Ollama service request timed out.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Ollama service.");
            return null;
        }
    }

    private string CleanModelReasoning(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return caption;

        // Remove <think>...</think> blocks (some models use this format)
        caption = Regex.Replace(caption, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();

        // Remove lines that start with ":" (reasoning fragments)
        caption = Regex.Replace(caption, @"^:.*$", "", RegexOptions.Multiline).Trim();

        // Remove common reasoning patterns in Russian
        var reasoningPatterns = new[]
        {
            @"^(Хорошо[,.]?)\s*",
            @"^(Начну с того, что|Мне нужно|Сначала посмотрю)[^.]*\.\s*",
            @"^(Проверяю|Собираю|Уточню|Надо|Важно|Видимо)[^.]*\.\s*",
            @"(ЗАПРЕЩЕНО|Запрещено)[^.]*\.\s*",
            @"Должен быть только[^.]*\.\s*",
            @"Не использую[^.]*\.\s*",
            @"В описании их нет[^.]*\.\s*",
            @"Нужно убедиться[^.]*\.\s*",
            @"^Зап\s*$",  // Truncated "Запрещено"
        };

        foreach (var pattern in reasoningPatterns)
        {
            caption = Regex.Replace(caption, pattern, "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        // Remove multiple newlines and clean up
        caption = Regex.Replace(caption, @"\n{2,}", "\n").Trim();

        // If result is too short or looks like garbage, return empty
        if (caption.Length < 20)
            return string.Empty;

        // Take only the last paragraph if there are multiple (often the final one is the actual description)
        var paragraphs = caption.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (paragraphs.Length > 1)
        {
            // Find the longest paragraph that looks like a description (not reasoning)
            var bestParagraph = paragraphs
                .Where(p => p.Length > 30 && !p.StartsWith("Проверяю") && !p.StartsWith(":"))
                .OrderByDescending(p => p.Length)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(bestParagraph))
                caption = bestParagraph.Trim();
        }

        return caption.Trim();
    }

    private bool IsArtwork(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return false;

        var lowerCaption = caption.ToLower();

        // English keywords
        var englishArtworkKeywords = new[]
        {
            "painting", "portrait", "sculpture", "statue", "canvas",
            "artwork", "masterpiece", "fresco", "mural", "drawing",
            "museum", "gallery", "exhibit", "art collection"
        };

        // Russian keywords (картина, скульптура, портрет, музей, галерея, etc.)
        var russianArtworkKeywords = new[]
        {
            "картин", "скульптур", "портрет", "статуя", "полотн",
            "шедевр", "фреск", "музе", "галере", "выставк",
            "произведение искусства", "живопис", "холст"
        };

        // Check if caption contains artwork indicators
        foreach (var keyword in englishArtworkKeywords.Concat(russianArtworkKeywords))
        {
            if (lowerCaption.Contains(keyword))
            {
                return true;
            }
        }

        return false;
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

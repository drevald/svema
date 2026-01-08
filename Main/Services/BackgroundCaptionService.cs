using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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

            // Wait 60 seconds before checking again (captions are slower than face detection)
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }

        _logger.LogInformation("Background Caption Service is stopping.");
    }

    private async Task GenerateCaptionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            // Get or create bot user
            var botUser = await GetOrCreateBotUserAsync(dbContext, stoppingToken);
            if (botUser == null)
            {
                _logger.LogWarning("Failed to get or create bot user.");
                return;
            }

            // Find shots without captions from this bot (limit to 5 per batch)
            var shotsToCaption = await dbContext.Shots
                .Where(s => !dbContext.ShotComments.Any(sc => sc.ShotId == s.ShotId && sc.AuthorId == botUser.UserId))
                .Where(s => s.FullScreen != null || s.Preview != null)
                .OrderByDescending(s => s.ShotId)
                .Take(5)
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
                    // Get image bytes (prefer FullScreen over Preview)
                    var imageBytes = shot.FullScreen ?? shot.Preview;
                    if (imageBytes == null)
                    {
                        _logger.LogWarning($"Shot {shot.ShotId}: No image data available.");
                        failed++;
                        continue;
                    }

                    // Call caption service
                    var caption = await CallCaptionServiceAsync(imageBytes, stoppingToken);
                    if (string.IsNullOrEmpty(caption))
                    {
                        _logger.LogWarning($"Shot {shot.ShotId}: Caption service returned empty caption.");
                        failed++;
                        continue;
                    }

                    // Create comment
                    var comment = new ShotComment
                    {
                        AuthorId = botUser.UserId,
                        AuthorUsername = _botUsername,
                        ShotId = shot.ShotId,
                        Timestamp = DateTime.UtcNow,
                        Text = caption
                    };

                    dbContext.ShotComments.Add(comment);
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
        // Check if bot user exists
        var botUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == _botUsername, stoppingToken);

        if (botUser != null)
            return botUser;

        // Create bot user
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

            // Convert image to base64
            var base64Image = Convert.ToBase64String(imageBytes);
            var imageDataUrl = $"data:image/jpeg;base64,{base64Image}";

            // Create Ollama chat completion request (OpenAI-compatible API)
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
                            new { type = "text", text = "Ты — помощник, который всегда отвечает по-русски.\nОпиши что ты видишь на этой фотографии.\nБудь фактическим и объективным. Будь кратким." },
                            new { type = "image_url", image_url = new { url = imageDataUrl } }
                        }
                    }
                },
                max_tokens = 100
            };

            var requestJson = JsonSerializer.Serialize(request);
            _logger.LogInformation($"Sending request to Ollama with model: {_modelName}");

            var jsonContent = new StringContent(
                requestJson,
                Encoding.UTF8,
                "application/json");

            // Call Ollama API
            var response = await httpClient.PostAsync(
                $"{_captionServiceUrl}/v1/chat/completions",
                jsonContent,
                stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(stoppingToken);
                _logger.LogWarning($"Ollama service returned status code: {response.StatusCode}, Body: {errorContent}");
                return null;
            }

            var result = await response.Content.ReadAsStringAsync(stoppingToken);
            _logger.LogInformation($"Ollama raw response: {result?.Substring(0, Math.Min(500, result?.Length ?? 0))}");

            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(result);
            var caption = ollamaResponse?.Choices?[0]?.Message?.Content?.Trim();

            _logger.LogInformation($"Parsed caption - Choices count: {ollamaResponse?.Choices?.Length ?? 0}, Content: '{caption}'");

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
    }
}

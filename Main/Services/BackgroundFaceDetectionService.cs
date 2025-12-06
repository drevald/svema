using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Data;

namespace Services;

public class BackgroundFaceDetectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundFaceDetectionService> _logger;

    public BackgroundFaceDetectionService(IServiceProvider serviceProvider, ILogger<BackgroundFaceDetectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Face Detection Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNewShotsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing background face detection.");
            }

            // Wait for a while before checking again
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("Background Face Detection Service is stopping.");
    }

    private async Task ProcessNewShotsAsync(CancellationToken stoppingToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var faceDetectionService = scope.ServiceProvider.GetRequiredService<FaceDetectionService>();
            var faceClusteringService = scope.ServiceProvider.GetRequiredService<FaceClusteringService>();

            _logger.LogDebug("Checking for shots to process...");

            var shotsToProcess = await context.Shots
                .Where(s => !s.IsFaceProcessed)
                .OrderByDescending(s => s.DateUploaded)
                .Take(10)
                .Select(s => s.ShotId)
                .ToListAsync(stoppingToken);

            if (shotsToProcess.Any())
            {
                _logger.LogInformation($"Found {shotsToProcess.Count} shots to process for face detection.");

                foreach (var shotId in shotsToProcess)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        await faceDetectionService.DetectAndStoreFacesAsync(shotId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to process face detection for shot {shotId}");
                    }
                }
            }

            // Trigger clustering for all users (runs even if no new shots were processed)
            // _logger.LogInformation("Face detection batch complete. Running clustering...");
            var users = await context.Users.ToListAsync(stoppingToken);

            foreach (var user in users)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    // We can optimize this by checking if there are unassigned faces first, 
                    // but the service method does that internally too.
                    var clustersCreated = await faceClusteringService.ClusterUnassignedFacesAsync(user.UserId);
                    if (clustersCreated > 0)
                    {
                        _logger.LogInformation($"Created {clustersCreated} new person cluster(s) for user {user.Username}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to cluster faces for user {user.Username}");
                }
            }
        }
    }
}

using System.Threading.Tasks;
using Data;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Services;

public class ClusteringSettingsService
{
    private readonly ApplicationDbContext _context;

    public ClusteringSettingsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ClusteringSettings> GetOrCreateSettingsAsync(int userId)
    {
        var settings = await _context.ClusteringSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (settings == null)
        {
            settings = new ClusteringSettings
            {
                UserId = userId,
                Preset = ClusteringPreset.Balanced
            };
            ApplyPreset(settings, ClusteringPreset.Balanced);
            _context.ClusteringSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }

    public async Task<ClusteringSettings> UpdateSettingsAsync(int userId, ClusteringPreset preset, float? threshold = null, int? minFaces = null, int? minSize = null, float? minQuality = null, float? autoMerge = null)
    {
        var settings = await GetOrCreateSettingsAsync(userId);

        settings.Preset = preset;

        if (preset == ClusteringPreset.Custom)
        {
            // Custom: user sets all parameters manually
            if (threshold.HasValue) settings.SimilarityThreshold = threshold.Value;
            if (minFaces.HasValue) settings.MinFacesPerPerson = minFaces.Value;
            if (minSize.HasValue) settings.MinFaceSize = minSize.Value;
            if (minQuality.HasValue) settings.MinFaceQuality = minQuality.Value;
            if (autoMerge.HasValue) settings.AutoMergeThreshold = autoMerge.Value;
        }
        else
        {
            // Apply preset values
            ApplyPreset(settings, preset);
        }

        await _context.SaveChangesAsync();
        return settings;
    }

    public async Task<bool> ToggleProcessingSuspendedAsync(int userId)
    {
        var settings = await GetOrCreateSettingsAsync(userId);
        settings.IsFaceProcessingSuspended = !settings.IsFaceProcessingSuspended;
        await _context.SaveChangesAsync();
        return settings.IsFaceProcessingSuspended;
    }

    private void ApplyPreset(ClusteringSettings settings, ClusteringPreset preset)
    {
        switch (preset)
        {
            case ClusteringPreset.Conservative:
                settings.SimilarityThreshold = 0.30f;
                settings.MinFacesPerPerson = 3;
                settings.MinFaceSize = 100;
                settings.MinFaceQuality = 0.4f;
                settings.AutoMergeThreshold = 0.85f;
                break;

            case ClusteringPreset.Balanced:
                settings.SimilarityThreshold = 0.23f;
                settings.MinFacesPerPerson = 2;
                settings.MinFaceSize = 80;
                settings.MinFaceQuality = 0.3f;
                settings.AutoMergeThreshold = 0.80f;
                break;

            case ClusteringPreset.Aggressive:
                settings.SimilarityThreshold = 0.15f;
                settings.MinFacesPerPerson = 1;
                settings.MinFaceSize = 60;
                settings.MinFaceQuality = 0.2f;
                settings.AutoMergeThreshold = 0.75f;
                break;

            case ClusteringPreset.NoiseTolerant:
                settings.SimilarityThreshold = 0.25f;
                settings.MinFacesPerPerson = 2;
                settings.MinFaceSize = 100;
                settings.MinFaceQuality = 0.5f;
                settings.AutoMergeThreshold = 0.82f;
                break;

            case ClusteringPreset.Custom:
                // Don't change anything, user controls all
                break;
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

public class ClusteringSettings
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    // Preset selection
    public ClusteringPreset Preset { get; set; } = ClusteringPreset.Balanced;

    // Core clustering parameters
    public float SimilarityThreshold { get; set; } = 0.23f;
    public int MinFacesPerPerson { get; set; } = 2;
    public int MinFaceSize { get; set; } = 80;
    public float MinFaceQuality { get; set; } = 0.3f;
    public float AutoMergeThreshold { get; set; } = 0.80f;

    // Processing control
    public bool IsFaceProcessingSuspended { get; set; } = false;

    [NotMapped]
    public string PresetName => Preset.ToString();

    [NotMapped]
    public string PresetDescription => Preset switch
    {
        ClusteringPreset.Conservative => "High accuracy, may split same person into multiple groups",
        ClusteringPreset.Balanced => "Recommended for most users - good balance of accuracy and grouping",
        ClusteringPreset.Aggressive => "Groups similar faces aggressively, may merge different people",
        ClusteringPreset.NoiseTolerant => "Best for mixed quality photos with background people",
        ClusteringPreset.Custom => "Manual configuration of all parameters",
        _ => ""
    };
}

public enum ClusteringPreset
{
    Conservative,
    Balanced,
    Aggressive,
    NoiseTolerant,
    Custom
}

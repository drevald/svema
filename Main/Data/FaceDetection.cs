using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Data;

#nullable enable
[Table("face_detections")]
public class FaceDetection
{
    [Column("id")]
    public int FaceDetectionId { get; set; }

    [Column("shot_id")]
    public int ShotId { get; set; }

    [JsonIgnore]
    public Shot? Shot { get; set; }

    [Column("x")]
    public int X { get; set; }

    [Column("y")]
    public int Y { get; set; }

    [Column("width")]
    public int Width { get; set; }

    [Column("height")]
    public int Height { get; set; }

    [Column("person_id")]
    public int? PersonId { get; set; }

    [JsonIgnore]
    public Person? Person { get; set; }

    [Column("is_confirmed")]
    public bool IsConfirmed { get; set; }

    [Column("detected_at")]
    public DateTime DetectedAt { get; set; }

    [Column("quality")]
    public float Quality { get; set; } = 0.5f; // Blur/sharpness quality score (0-1, higher is better)

    [JsonIgnore]
    public FaceEncoding? FaceEncoding { get; set; }
}
#nullable disable

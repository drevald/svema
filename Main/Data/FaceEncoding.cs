using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Data;

[Table("face_encodings")]
public class FaceEncoding
{
    [Column("id")]
    public int FaceEncodingId { get; set; }

    [Column("face_detection_id")]
    public int FaceDetectionId { get; set; }

    [JsonIgnore]
    public FaceDetection FaceDetection { get; set; }

    [Column("encoding")]
    public byte[] Encoding { get; set; }
}

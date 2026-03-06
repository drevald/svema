using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Microsoft.EntityFrameworkCore;

namespace FaceDiagnostic;

/// <summary>
/// Diagnostic tool to analyze face cluster quality and detect bloated persons
/// </summary>
public class PersonDispersionAnalyzer
{
    private readonly ApplicationDbContext _context;

    public PersonDispersionAnalyzer(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PersonClusterStats> AnalyzePersonAsync(int personId)
    {
        var person = await _context.Persons
            .Include(p => p.FaceDetections)
            .ThenInclude(fd => fd.FaceEncoding)
            .FirstOrDefaultAsync(p => p.PersonId == personId);

        if (person == null)
            return null;

        var encodings = person.FaceDetections
            .Where(fd => fd.FaceEncoding != null)
            .Select(fd => ByteArrayToFloatArray(fd.FaceEncoding.Encoding))
            .ToList();

        if (encodings.Count < 2)
            return new PersonClusterStats { PersonId = personId, FaceCount = encodings.Count };

        // Calculate centroid
        var centroid = CalculateCentroid(encodings);

        // Calculate all pairwise similarities
        var pairwiseSimilarities = new List<float>();
        var similarityToCentroid = new List<float>();

        for (int i = 0; i < encodings.Count; i++)
        {
            // Similarity to centroid
            similarityToCentroid.Add(CosineSimilarity(encodings[i], centroid));

            // Pairwise similarities
            for (int j = i + 1; j < encodings.Count; j++)
            {
                pairwiseSimilarities.Add(CosineSimilarity(encodings[i], encodings[j]));
            }
        }

        return new PersonClusterStats
        {
            PersonId = personId,
            FaceCount = encodings.Count,

            // Centroid-based metrics
            AvgSimilarityToCentroid = similarityToCentroid.Average(),
            MinSimilarityToCentroid = similarityToCentroid.Min(),
            MaxSimilarityToCentroid = similarityToCentroid.Max(),
            StdDevSimilarityToCentroid = CalculateStdDev(similarityToCentroid),

            // Pairwise metrics (more accurate for detecting bloat)
            AvgPairwiseSimilarity = pairwiseSimilarities.Average(),
            MinPairwiseSimilarity = pairwiseSimilarities.Min(),
            MaxPairwiseSimilarity = pairwiseSimilarities.Max(),
            StdDevPairwiseSimilarity = CalculateStdDev(pairwiseSimilarities),

            // Quality indicators
            Dispersion = CalculateDispersion(encodings, centroid),
            IsPotentiallyBloated = IsPotentiallyBloated(pairwiseSimilarities, encodings.Count)
        };
    }

    private float CalculateDispersion(List<float[]> encodings, float[] centroid)
    {
        // Calculate average squared distance from centroid
        float sumSquaredDist = 0;
        foreach (var enc in encodings)
        {
            float dist = 1 - CosineSimilarity(enc, centroid); // Convert similarity to distance
            sumSquaredDist += dist * dist;
        }
        return (float)Math.Sqrt(sumSquaredDist / encodings.Count);
    }

    private bool IsPotentiallyBloated(List<float> pairwiseSimilarities, int faceCount)
    {
        // A cluster is potentially bloated if:
        // 1. It has many faces (>10) AND
        // 2. Minimum pairwise similarity is very low (<0.15) OR
        // 3. Average pairwise similarity is low (<0.30) OR
        // 4. High variance in pairwise similarities (std dev > 0.20)

        if (faceCount <= 10)
            return false;

        float min = pairwiseSimilarities.Min();
        float avg = pairwiseSimilarities.Average();
        float stdDev = CalculateStdDev(pairwiseSimilarities);

        return min < 0.15f || avg < 0.30f || stdDev > 0.20f;
    }

    private float CalculateStdDev(List<float> values)
    {
        if (values.Count < 2) return 0;

        float avg = values.Average();
        float sumSquaredDiff = values.Sum(v => (v - avg) * (v - avg));
        return (float)Math.Sqrt(sumSquaredDiff / values.Count);
    }

    private float[] CalculateCentroid(List<float[]> vectors)
    {
        int dim = vectors[0].Length;
        var centroid = new float[dim];
        foreach (var v in vectors)
        {
            for (int i = 0; i < dim; i++) centroid[i] += v[i];
        }
        for (int i = 0; i < dim; i++) centroid[i] /= vectors.Count;
        return centroid;
    }

    private float[] ByteArrayToFloatArray(byte[] bytes)
    {
        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0) return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}

public class PersonClusterStats
{
    public int PersonId { get; set; }
    public int FaceCount { get; set; }

    // Centroid-based metrics
    public float AvgSimilarityToCentroid { get; set; }
    public float MinSimilarityToCentroid { get; set; }
    public float MaxSimilarityToCentroid { get; set; }
    public float StdDevSimilarityToCentroid { get; set; }

    // Pairwise metrics (better for detecting bloat)
    public float AvgPairwiseSimilarity { get; set; }
    public float MinPairwiseSimilarity { get; set; }
    public float MaxPairwiseSimilarity { get; set; }
    public float StdDevPairwiseSimilarity { get; set; }

    // Quality metrics
    public float Dispersion { get; set; }
    public bool IsPotentiallyBloated { get; set; }

    public override string ToString()
    {
        return $@"
Person #{PersonId} Cluster Analysis:
==================================
Face Count: {FaceCount}
Bloated: {(IsPotentiallyBloated ? "YES ⚠️" : "No")}

Centroid-Based Metrics:
  Avg similarity to centroid: {AvgSimilarityToCentroid:F3}
  Min similarity to centroid: {MinSimilarityToCentroid:F3}
  Max similarity to centroid: {MaxSimilarityToCentroid:F3}
  Std dev (centroid): {StdDevSimilarityToCentroid:F3}
  Dispersion: {Dispersion:F3}

Pairwise Similarity Metrics (All faces compared):
  Avg pairwise similarity: {AvgPairwiseSimilarity:F3}
  Min pairwise similarity: {MinPairwiseSimilarity:F3} {(MinPairwiseSimilarity < 0.15f ? "⚠️ VERY LOW!" : "")}
  Max pairwise similarity: {MaxPairwiseSimilarity:F3}
  Std dev (pairwise): {StdDevPairwiseSimilarity:F3} {(StdDevPairwiseSimilarity > 0.20f ? "⚠️ HIGH VARIANCE!" : "")}

Interpretation:
  - Good cluster: Avg pairwise > 0.40, Min > 0.25
  - Suspect cluster: Avg pairwise < 0.30, Min < 0.15
  - Bloated cluster: Min < 0.10 (unrelated faces present)
";
    }
}

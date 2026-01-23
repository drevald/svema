using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services;

public class FaceClusteringService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FaceClusteringService> _logger;

    public FaceClusteringService(ApplicationDbContext context, ILogger<FaceClusteringService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> ClusterUnassignedFacesAsync(int userId)
    {
        // Load user's clustering settings
        var settings = await GetOrCreateSettingsAsync(userId);

        // Check if processing is suspended
        if (settings.IsFaceProcessingSuspended)
        {
            _logger.LogDebug($"[Face Clustering] Face processing is suspended for user {userId}");
            return 0;
        }

        // 1. Get unassigned faces
        var unassignedFaces = await _context.FaceDetections
            .Include(fd => fd.Shot)
            .ThenInclude(s => s.Album)
            .Include(fd => fd.FaceEncoding)
            .Where(fd => fd.PersonId == null && fd.Shot.Album.User.UserId == userId && fd.FaceEncoding != null)
            .ToListAsync();

        if (!unassignedFaces.Any()) return 0;

        _logger.LogInformation($"[Face Clustering] Starting clustering for {unassignedFaces.Count} unassigned faces");
        _logger.LogInformation($"[Face Clustering] Settings - Preset: {settings.Preset}, Threshold: {settings.SimilarityThreshold}, MinFaces: {settings.MinFacesPerPerson}, MinSize: {settings.MinFaceSize}px");
        _logger.LogInformation($"[Face Clustering] Quality - MaxDispersion: {settings.MaxDispersion}, MinPairwise: {settings.MinPairwiseSimilarity}");

        // VALIDATE: All encodings must have same dimension
        ValidateEncodingDimensions();

        // 2. Get existing persons and their confirmed faces (to build centroids)
        var existingPersons = await _context.Persons
            .Include(p => p.FaceDetections)
            .ThenInclude(fd => fd.FaceEncoding)
            .Where(p => p.FaceDetections.Any(fd => fd.FaceEncoding != null))
            .ToListAsync();

        var personCentroids = new Dictionary<int, float[]>();
        foreach (var person in existingPersons)
        {
            var encodings = person.FaceDetections
                .Where(fd => fd.FaceEncoding != null)
                .Select(fd => ByteArrayToFloatArray(fd.FaceEncoding.Encoding))
                .ToList();

            if (encodings.Any())
            {
                personCentroids[person.PersonId] = CalculateCentroid(encodings);
            }
        }

        // 3. Prepare unassigned faces (apply quality and size filters)
        var faceData = unassignedFaces.Select(fd => new
        {
            Detection = fd,
            Encoding = ByteArrayToFloatArray(fd.FaceEncoding.Encoding)
        })
        .Where(f => f.Detection.Width >= settings.MinFaceSize && f.Detection.Height >= settings.MinFaceSize)
        .ToList();

        int afterSizeFilter = faceData.Count;

        // Filter by quality (blur detection)
        faceData = faceData.Where(f => f.Detection.Quality >= settings.MinFaceQuality).ToList();

        int filteredByQuality = afterSizeFilter - faceData.Count;

        _logger.LogInformation($"After size filter ({settings.MinFaceSize}px): {afterSizeFilter} faces remaining");
        _logger.LogInformation($"After quality filter (>= {settings.MinFaceQuality:F2}): {faceData.Count} faces remaining (filtered {filteredByQuality} blurry faces)");

        float similarityThreshold = settings.SimilarityThreshold;
        int minFacesPerPerson = settings.MinFacesPerPerson;
        float maxDispersion = settings.MaxDispersion;
        float minPairwiseSimilarity = settings.MinPairwiseSimilarity;
        int newClustersCount = 0;
        int rejectedByQuality = 0; // Track faces rejected by quality checks

        // New clusters: store ALL face encodings, not just centroid
        var newClusters = new List<(List<FaceDetection> Faces, List<float[]> Encodings)>();

        foreach (var face in faceData)
        {
            int? bestPersonId = null;
            int? bestClusterIndex = null;
            float bestScore = -1f;

            // A. Try to match with existing persons - use centroid similarity + dispersion check
            foreach (var person in existingPersons)
            {
                // SAME-SHOT CONSTRAINT: Skip if this person already has a face from the same shot
                if (person.FaceDetections.Any(fd => fd.ShotId == face.Detection.ShotId))
                {
                    _logger.LogDebug($"Skipping Person {person.PersonId} - already has a face from Shot {face.Detection.ShotId}");
                    continue;
                }

                // Use pre-calculated centroid
                if (!personCentroids.ContainsKey(person.PersonId))
                    continue;

                float similarity = CosineSimilarity(face.Encoding, personCentroids[person.PersonId]);

                if (similarity >= similarityThreshold)
                {
                    // QUALITY CHECK: Verify this face won't bloat the cluster
                    var personEncodings = person.FaceDetections
                        .Where(fd => fd.FaceEncoding != null)
                        .Select(fd => ByteArrayToFloatArray(fd.FaceEncoding.Encoding))
                        .ToList();

                    if (WouldBloatCluster(face.Encoding, personEncodings, maxDispersion, minPairwiseSimilarity, person.PersonId))
                    {
                        _logger.LogDebug($"[Quality] Rejected face {face.Detection.FaceDetectionId} from Person {person.PersonId} - would bloat cluster (similarity: {similarity:F3})");
                        rejectedByQuality++;
                        continue;
                    }

                    if (similarity > bestScore)
                    {
                        bestScore = similarity;
                        bestPersonId = person.PersonId;
                    }
                }
            }

            // B. Try to match with new clusters - use centroid similarity + dispersion check
            for (int i = 0; i < newClusters.Count; i++)
            {
                // SAME-SHOT CONSTRAINT: Skip if this cluster already has a face from the same shot
                if (newClusters[i].Faces.Any(f => f.ShotId == face.Detection.ShotId))
                {
                    _logger.LogDebug($"Skipping cluster {i} - already has a face from Shot {face.Detection.ShotId}");
                    continue;
                }

                // Calculate centroid for this cluster
                var clusterCentroid = CalculateCentroid(newClusters[i].Encodings);
                float similarity = CosineSimilarity(face.Encoding, clusterCentroid);

                if (similarity >= similarityThreshold)
                {
                    // QUALITY CHECK: Verify this face won't bloat the cluster
                    if (WouldBloatCluster(face.Encoding, newClusters[i].Encodings, maxDispersion, minPairwiseSimilarity, -1))
                    {
                        _logger.LogDebug($"[Quality] Rejected face {face.Detection.FaceDetectionId} from cluster {i} - would bloat cluster (similarity: {similarity:F3})");
                        rejectedByQuality++;
                        continue;
                    }

                    if (similarity > bestScore)
                    {
                        bestScore = similarity;
                        bestClusterIndex = i;
                        bestPersonId = null; // Reset person match if cluster is better
                    }
                }
            }

            if (bestPersonId.HasValue)
            {
                // Assign to existing person
                face.Detection.PersonId = bestPersonId.Value;
                _logger.LogInformation($"[Face Clustering] Face {face.Detection.FaceDetectionId} from shot {face.Detection.ShotId} assigned to person {bestPersonId.Value}");
            }
            else if (bestClusterIndex.HasValue)
            {
                // Add to new cluster
                var cluster = newClusters[bestClusterIndex.Value];
                cluster.Faces.Add(face.Detection);
                cluster.Encodings.Add(face.Encoding);
                newClusters[bestClusterIndex.Value] = cluster;
                _logger.LogDebug($"[Face Clustering] Added face {face.Detection.FaceDetectionId} to new cluster {bestClusterIndex.Value} (similarity: {bestScore:F3})");
            }
            else
            {
                // Create new cluster
                newClusters.Add((new List<FaceDetection> { face.Detection }, new List<float[]> { face.Encoding }));
                newClustersCount++;
                _logger.LogDebug($"[Face Clustering] Created new cluster for face {face.Detection.FaceDetectionId} (best similarity was {bestScore:F3}, threshold: {similarityThreshold})");
            }
        }

        // 4. Save changes for assigned faces (PersonId was set in the loop)
        await _context.SaveChangesAsync();

        // 5. Create new persons for new clusters (only if >= minFacesPerPerson)
        var validClusters = newClusters.Where(c => c.Faces.Count >= minFacesPerPerson).ToList();
        var invalidClusters = newClusters.Where(c => c.Faces.Count < minFacesPerPerson).ToList();
        var skippedFaces = invalidClusters.Sum(c => c.Faces.Count);

        _logger.LogInformation($"[Face Clustering] Summary: {newClusters.Count} total clusters, {validClusters.Count} valid (>= {minFacesPerPerson} faces), {invalidClusters.Count} invalid (< {minFacesPerPerson} faces)");

        if (skippedFaces > 0)
        {
            _logger.LogWarning($"[Face Clustering] Skipped creating persons for {skippedFaces} faces in {invalidClusters.Count} small clusters (< {minFacesPerPerson} faces per cluster)");
            foreach (var cluster in invalidClusters)
            {
                _logger.LogDebug($"[Face Clustering] Small cluster: {cluster.Faces.Count} face(s) - FaceIds: {string.Join(", ", cluster.Faces.Select(f => f.FaceDetectionId))}");
            }
        }

        foreach (var cluster in validClusters)
        {
            var person = new Person
            {
                FirstName = "Person",
                LastName = $"#{DateTime.UtcNow.Ticks % 100000}", // Temporary placeholder
                CreatedAt = DateTime.UtcNow
            };
            _context.Persons.Add(person);
            await _context.SaveChangesAsync(); // Save to get PersonId

            foreach (var fd in cluster.Faces)
            {
                fd.PersonId = person.PersonId;
            }

            // Update name with actual ID for cleanliness
            person.LastName = $"#{person.PersonId}";

            _logger.LogInformation($"[Face Clustering] Person {person.PersonId} created");
        }

        await _context.SaveChangesAsync();

        // Final summary
        int assignedToExisting = faceData.Count(f => f.Detection.PersonId != null);
        int inNewClusters = newClusters.Sum(c => c.Faces.Count);
        int totalProcessed = faceData.Count;

        _logger.LogInformation($"[Face Clustering] === CLUSTERING COMPLETE ===");
        _logger.LogInformation($"[Face Clustering] Total faces processed: {totalProcessed}");
        _logger.LogInformation($"[Face Clustering] Assigned to existing persons: {assignedToExisting}");
        _logger.LogInformation($"[Face Clustering] In new clusters: {inNewClusters} faces in {newClusters.Count} clusters");
        _logger.LogInformation($"[Face Clustering] New persons created: {validClusters.Count}");
        _logger.LogInformation($"[Face Clustering] Faces in small clusters (not creating persons): {skippedFaces}");
        _logger.LogInformation($"[Face Clustering] Rejected by quality checks: {rejectedByQuality}");

        return validClusters.Count;
    }

    /// <summary>
    /// Checks if adding a new face would bloat the cluster beyond acceptable quality thresholds
    /// </summary>
    private bool WouldBloatCluster(float[] newFace, List<float[]> clusterEncodings, float maxDispersion, float minPairwiseSimilarity, int personId)
    {
        // Always accept the first face
        if (clusterEncodings.Count == 0)
            return false;

        // For very small clusters (1-2 faces), be more lenient
        if (clusterEncodings.Count <= 2)
            return false;

        // CHECK 1: Minimum pairwise similarity
        // The new face must be similar to ALL existing faces (not just the centroid)
        float minSimilarity = float.MaxValue;
        foreach (var existing in clusterEncodings)
        {
            float sim = CosineSimilarity(newFace, existing);
            if (sim < minSimilarity)
                minSimilarity = sim;
        }

        if (minSimilarity < minPairwiseSimilarity)
        {
            if (personId > 0)
                _logger.LogDebug($"[Quality] Person {personId}: Min pairwise similarity {minSimilarity:F3} < threshold {minPairwiseSimilarity:F3}");
            return true; // Would introduce a face that's too different from at least one existing face
        }

        // CHECK 2: Dispersion check
        // Calculate what the dispersion would be if we add this face
        var extendedCluster = clusterEncodings.ToList();
        extendedCluster.Add(newFace);
        var centroid = CalculateCentroid(extendedCluster);
        float dispersion = CalculateDispersion(extendedCluster, centroid);

        if (dispersion > maxDispersion)
        {
            if (personId > 0)
                _logger.LogDebug($"[Quality] Person {personId}: Dispersion {dispersion:F3} > max {maxDispersion:F3}");
            return true; // Would make cluster too spread out
        }

        return false; // Quality checks passed
    }

    /// <summary>
    /// Calculates cluster dispersion (average distance from centroid)
    /// </summary>
    private float CalculateDispersion(List<float[]> encodings, float[] centroid)
    {
        if (encodings.Count == 0) return 0;

        float sumDist = 0;
        foreach (var enc in encodings)
        {
            // Convert cosine similarity to distance: distance = 1 - similarity
            float similarity = CosineSimilarity(enc, centroid);
            float dist = 1 - similarity;
            sumDist += dist;
        }
        return sumDist / encodings.Count;
    }

    private void ValidateEncodingDimensions()
    {
        var dimensions = _context.FaceEncodings
            .Select(fe => fe.Encoding.Length / 4) // 4 bytes per float
            .Distinct()
            .ToList();

        if (dimensions.Count > 1)
        {
            _logger.LogError($"FATAL: Multiple encoding dimensions detected: {string.Join(", ", dimensions)}. Clustering is invalid!");
            throw new InvalidOperationException($"Cannot cluster faces with different embedding dimensions: {string.Join(", ", dimensions)}. All faces must use the same model.");
        }

        if (dimensions.Any())
        {
            _logger.LogInformation($"All encodings validated: {dimensions[0]} dimensions");
        }
    }

    private float[] CalculateCentroid(List<float[]> vectors)
    {
        int dim = vectors[0].Length;
        var centroid = new float[dim];
        foreach (var v in vectors)
        {
            for (int i = 0; i < dim; i++) centroid[i] += v[i];
        }

        // Normalize
        for (int i = 0; i < dim; i++) centroid[i] /= vectors.Count;

        // Re-normalize to unit length (important for cosine similarity)
        // Although average of unit vectors is not unit vector, direction matters.
        // Cosine similarity only cares about direction.
        return centroid;
    }

    private float[] RecalculateCentroid(float[] currentCentroid, int currentCount, float[] newVector)
    {
        // weighted average: (old * N + new) / (N + 1)
        int dim = currentCentroid.Length;
        var newCentroid = new float[dim];
        for (int i = 0; i < dim; i++)
        {
            newCentroid[i] = (currentCentroid[i] * currentCount + newVector[i]) / (currentCount + 1);
        }
        return newCentroid;
    }

    private float[] ByteArrayToFloatArray(byte[] bytes)
    {
        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

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

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    public async Task<Person> SuggestPersonForFaceAsync(int faceDetectionId)
    {
        // TODO: Implement suggestion logic based on similarity to confirmed faces.
        return null;
    }

    public async Task<List<FaceDetection>> GetUnconfirmedFacesAsync(int userId)
    {
        return await _context.FaceDetections
            .Include(fd => fd.Shot)
            .Include(fd => fd.Person)
            .Where(fd => fd.Shot.Album.User.UserId == userId && fd.PersonId != null && !fd.IsConfirmed && !fd.Shot.NoFaces)
            .OrderByDescending(fd => fd.DetectedAt)
            .ToListAsync();
    }

    public async Task<List<FaceDetection>> GetUnassignedFacesAsync(int userId)
    {
        return await _context.FaceDetections
           .Include(fd => fd.Shot)
           .Where(fd => fd.Shot.Album.User.UserId == userId && fd.PersonId == null && !fd.Shot.NoFaces)
           .OrderByDescending(fd => fd.DetectedAt)
           .ToListAsync();
    }

    /// <summary>
    /// Analyzes all persons and identifies bloated clusters that need manual review
    /// </summary>
    public async Task<List<BloatedPersonInfo>> FindBloatedPersonsAsync(int userId)
    {
        var persons = await _context.Persons
            .Include(p => p.FaceDetections)
            .ThenInclude(fd => fd.FaceEncoding)
            .Where(p => p.FaceDetections.Any(fd => fd.Shot.Album.User.UserId == userId))
            .ToListAsync();

        var bloatedPersons = new List<BloatedPersonInfo>();

        foreach (var person in persons)
        {
            var encodings = person.FaceDetections
                .Where(fd => fd.FaceEncoding != null)
                .Select(fd => ByteArrayToFloatArray(fd.FaceEncoding.Encoding))
                .ToList();

            if (encodings.Count < 3)
                continue; // Skip small clusters

            // Calculate all pairwise similarities
            var pairwiseSimilarities = new List<float>();
            for (int i = 0; i < encodings.Count; i++)
            {
                for (int j = i + 1; j < encodings.Count; j++)
                {
                    pairwiseSimilarities.Add(CosineSimilarity(encodings[i], encodings[j]));
                }
            }

            // Calculate dispersion
            var centroid = CalculateCentroid(encodings);
            float dispersion = CalculateDispersion(encodings, centroid);

            // Determine if bloated
            float minPairwise = pairwiseSimilarities.Min();
            float avgPairwise = pairwiseSimilarities.Average();
            float stdDev = CalculateStdDev(pairwiseSimilarities);

            bool isBloated =
                minPairwise < 0.05f ||  // Some faces are completely unrelated
                (avgPairwise < 0.15f && encodings.Count > 15) || // Low average with many faces
                (dispersion > 0.60f && encodings.Count > 8) || // High dispersion
                (stdDev > 0.30f && encodings.Count > 15); // High variance with many faces

            if (isBloated)
            {
                bloatedPersons.Add(new BloatedPersonInfo
                {
                    PersonId = person.PersonId,
                    FirstName = person.FirstName,
                    LastName = person.LastName,
                    FaceCount = encodings.Count,
                    MinPairwiseSimilarity = minPairwise,
                    AvgPairwiseSimilarity = avgPairwise,
                    Dispersion = dispersion,
                    StdDevPairwise = stdDev,
                    Severity = CalculateSeverity(minPairwise, avgPairwise, dispersion, encodings.Count)
                });
            }
        }

        return bloatedPersons.OrderByDescending(p => p.Severity).ToList();
    }

    private float CalculateStdDev(List<float> values)
    {
        if (values.Count < 2) return 0;
        float avg = values.Average();
        float sumSquaredDiff = values.Sum(v => (v - avg) * (v - avg));
        return (float)Math.Sqrt(sumSquaredDiff / values.Count);
    }

    private string CalculateSeverity(float minPairwise, float avgPairwise, float dispersion, int faceCount)
    {
        // Critical: Definitely bloated, unrelated faces present
        if (minPairwise < 0.02f || (minPairwise < 0.05f && faceCount > 30))
            return "CRITICAL";

        // High: Very likely bloated
        if (minPairwise < 0.08f || avgPairwise < 0.12f || dispersion > 0.65f)
            return "HIGH";

        // Medium: Probably bloated
        if (minPairwise < 0.12f || avgPairwise < 0.18f || dispersion > 0.55f)
            return "MEDIUM";

        return "LOW";
    }

    public async Task<(List<FaceDetection> Faces, int TotalCount)> GetUnconfirmedFacesAsync(int userId, int page, int pageSize)
    {
        var settings = await GetOrCreateSettingsAsync(userId);

        var query = _context.FaceDetections
            .Include(fd => fd.Shot)
            .Include(fd => fd.Person)
            .Where(fd => fd.Shot.Album.User.UserId == userId
                      && fd.PersonId != null
                      && !fd.IsConfirmed
                      && !fd.Shot.NoFaces
                      && fd.Width >= settings.MinFaceSize
                      && fd.Height >= settings.MinFaceSize
                      && fd.Quality >= settings.MinFaceQuality)
            .OrderByDescending(fd => fd.DetectedAt);

        var totalCount = await query.CountAsync();
        var faces = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (faces, totalCount);
    }

    public async Task<(List<FaceDetection> Faces, int TotalCount)> GetUnassignedFacesAsync(int userId, int page, int pageSize)
    {
        var settings = await GetOrCreateSettingsAsync(userId);

        var query = _context.FaceDetections
            .Include(fd => fd.Shot)
            .Where(fd => fd.Shot.Album.User.UserId == userId
                      && fd.PersonId == null
                      && !fd.Shot.NoFaces
                      && fd.Width >= settings.MinFaceSize
                      && fd.Height >= settings.MinFaceSize
                      && fd.Quality >= settings.MinFaceQuality)
            .OrderByDescending(fd => fd.DetectedAt);

        var totalCount = await query.CountAsync();
        var faces = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (faces, totalCount);
    }

    private async Task<Models.ClusteringSettings> GetOrCreateSettingsAsync(int userId)
    {
        var settings = await _context.ClusteringSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (settings == null)
        {
            // Create default settings
            settings = new Models.ClusteringSettings
            {
                UserId = userId,
                Preset = Models.ClusteringPreset.Balanced,
                SimilarityThreshold = 0.23f,
                MinFacesPerPerson = 2,
                MinFaceSize = 40,
                MinFaceQuality = 0.3f,
                AutoMergeThreshold = 0.80f,
                MaxDispersion = 0.50f,
                MinPairwiseSimilarity = 0.08f,
                IsFaceProcessingSuspended = false
            };
            _context.ClusteringSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }
}

public class BloatedPersonInfo
{
    public int PersonId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int FaceCount { get; set; }
    public float MinPairwiseSimilarity { get; set; }
    public float AvgPairwiseSimilarity { get; set; }
    public float Dispersion { get; set; }
    public float StdDevPairwise { get; set; }
    public string Severity { get; set; } // CRITICAL, HIGH, MEDIUM, LOW
}

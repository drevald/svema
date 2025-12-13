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
        // 1. Get unassigned faces
        var unassignedFaces = await _context.FaceDetections
            .Include(fd => fd.Shot)
            .ThenInclude(s => s.Album)
            .Include(fd => fd.FaceEncoding)
            .Where(fd => fd.PersonId == null && fd.Shot.Album.User.UserId == userId && fd.FaceEncoding != null)
            .ToListAsync();

        if (!unassignedFaces.Any()) return 0;

        _logger.LogInformation($"Starting clustering for {unassignedFaces.Count} unassigned faces.");

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

        // 3. Prepare unassigned faces
        var faceData = unassignedFaces.Select(fd => new
        {
            Detection = fd,
            Encoding = ByteArrayToFloatArray(fd.FaceEncoding.Encoding)
        }).ToList();

        const float similarityThreshold = 0.85f; // Stricter threshold for higher accuracy
        const float minSimilarityWeight = 0.6f; // Weight for minimum similarity in scoring
        const float avgSimilarityWeight = 0.4f; // Weight for average similarity in scoring
        int newClustersCount = 0;

        // New clusters: store ALL face encodings, not just centroid
        var newClusters = new List<(List<FaceDetection> Faces, List<float[]> Encodings)>();

        foreach (var face in faceData)
        {
            int? bestPersonId = null;
            int? bestClusterIndex = null;
            float bestScore = -1f;

            // A. Try to match with existing persons - use weighted scoring (min + avg similarity)
            foreach (var person in existingPersons)
            {
                var personEncodings = person.FaceDetections
                    .Where(fd => fd.FaceEncoding != null)
                    .Select(fd => ByteArrayToFloatArray(fd.FaceEncoding.Encoding))
                    .ToList();

                // Calculate both minimum and average similarity
                float minSimilarity = 1.0f;
                float totalSimilarity = 0f;
                int count = 0;

                foreach (var encoding in personEncodings)
                {
                    var sim = CosineSimilarity(face.Encoding, encoding);
                    if (sim < minSimilarity) minSimilarity = sim;
                    totalSimilarity += sim;
                    count++;
                }

                float avgSimilarity = count > 0 ? totalSimilarity / count : 0f;
                float score = (minSimilarity * minSimilarityWeight) + (avgSimilarity * avgSimilarityWeight);

                if (score > bestScore && minSimilarity >= similarityThreshold)
                {
                    bestScore = score;
                    bestPersonId = person.PersonId;
                }
            }

            // B. Try to match with new clusters - use same weighted scoring
            for (int i = 0; i < newClusters.Count; i++)
            {
                float minSimilarity = 1.0f;
                float totalSimilarity = 0f;
                int count = 0;

                foreach (var encoding in newClusters[i].Encodings)
                {
                    var sim = CosineSimilarity(face.Encoding, encoding);
                    if (sim < minSimilarity) minSimilarity = sim;
                    totalSimilarity += sim;
                    count++;
                }

                float avgSimilarity = count > 0 ? totalSimilarity / count : 0f;
                float score = (minSimilarity * minSimilarityWeight) + (avgSimilarity * avgSimilarityWeight);

                if (score > bestScore && minSimilarity >= similarityThreshold)
                {
                    bestScore = score;
                    bestClusterIndex = i;
                    bestPersonId = null; // Reset person match if cluster is better
                }
            }

            if (bestPersonId.HasValue)
            {
                // Assign to existing person
                face.Detection.PersonId = bestPersonId.Value;
                _logger.LogInformation($"Assigned face {face.Detection.FaceDetectionId} to existing Person {bestPersonId.Value} (score: {bestScore:F3})");
            }
            else if (bestClusterIndex.HasValue)
            {
                // Add to new cluster
                var cluster = newClusters[bestClusterIndex.Value];
                cluster.Faces.Add(face.Detection);
                cluster.Encodings.Add(face.Encoding);
                newClusters[bestClusterIndex.Value] = cluster;
                _logger.LogInformation($"Added face {face.Detection.FaceDetectionId} to new cluster {bestClusterIndex.Value} (score: {bestScore:F3})");
            }
            else
            {
                // Create new cluster
                newClusters.Add((new List<FaceDetection> { face.Detection }, new List<float[]> { face.Encoding }));
                newClustersCount++;
                _logger.LogInformation($"Created new cluster for face {face.Detection.FaceDetectionId} (best score was {bestScore:F3}, threshold: {similarityThreshold})");
            }
        }

        // 4. Save changes for assigned faces (PersonId was set in the loop)
        await _context.SaveChangesAsync();

        // 5. Create new persons for new clusters
        foreach (var cluster in newClusters)
        {
            var person = new Person
            {
                FirstName = "Person",
                LastName = $"#{DateTime.UtcNow.Ticks % 100000}" // Temporary placeholder
            };
            _context.Persons.Add(person);
            await _context.SaveChangesAsync(); // Save to get PersonId

            foreach (var fd in cluster.Faces)
            {
                fd.PersonId = person.PersonId;
            }

            // Update name with actual ID for cleanliness
            person.LastName = $"#{person.PersonId}";

            _logger.LogInformation($"Created Person #{person.PersonId} with {cluster.Faces.Count} face(s).");
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation($"Clustering complete. Created {newClustersCount} new person(s) for user {userId}.");
        return newClustersCount;
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
            .Where(fd => fd.Shot.Album.User.UserId == userId && fd.PersonId != null && !fd.IsConfirmed)
            .OrderByDescending(fd => fd.DetectedAt)
            .ToListAsync();
    }

    public async Task<List<FaceDetection>> GetUnassignedFacesAsync(int userId)
    {
        return await _context.FaceDetections
           .Include(fd => fd.Shot)
           .Where(fd => fd.Shot.Album.User.UserId == userId && fd.PersonId == null)
           .OrderByDescending(fd => fd.DetectedAt)
           .ToListAsync();
    }
}

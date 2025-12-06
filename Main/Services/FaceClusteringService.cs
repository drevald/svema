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

        const float similarityThreshold = 0.40f; // Lowered from 0.45 for better recall
        int newClustersCount = 0;

        // New clusters we are building in this run: List of (Faces, Centroid)
        var newClusters = new List<(List<FaceDetection> Faces, float[] Centroid)>();

        foreach (var face in faceData)
        {
            int? bestPersonId = null;
            int? bestClusterIndex = null;
            float bestSimilarity = -1f;

            // A. Try to match with existing persons
            foreach (var kvp in personCentroids)
            {
                var sim = CosineSimilarity(face.Encoding, kvp.Value);
                if (sim > bestSimilarity && sim >= similarityThreshold)
                {
                    bestSimilarity = sim;
                    bestPersonId = kvp.Key;
                }
            }

            // B. Try to match with new clusters
            for (int i = 0; i < newClusters.Count; i++)
            {
                var sim = CosineSimilarity(face.Encoding, newClusters[i].Centroid);
                if (sim > bestSimilarity && sim >= similarityThreshold)
                {
                    bestSimilarity = sim;
                    bestClusterIndex = i;
                    bestPersonId = null; // Reset person match if cluster is better
                }
            }

            if (bestPersonId.HasValue)
            {
                // Assign to existing person
                face.Detection.PersonId = bestPersonId.Value;
                _logger.LogDebug($"Assigned face {face.Detection.FaceDetectionId} to existing Person {bestPersonId.Value} (similarity: {bestSimilarity:F3})");
            }
            else if (bestClusterIndex.HasValue)
            {
                // Add to new cluster
                var cluster = newClusters[bestClusterIndex.Value];
                cluster.Faces.Add(face.Detection);
                // Update centroid
                cluster.Centroid = RecalculateCentroid(cluster.Centroid, cluster.Faces.Count - 1, face.Encoding);
                newClusters[bestClusterIndex.Value] = cluster; // Update tuple in list
                _logger.LogDebug($"Added face {face.Detection.FaceDetectionId} to new cluster {bestClusterIndex.Value} (similarity: {bestSimilarity:F3})");
            }
            else
            {
                // Create new cluster
                newClusters.Add((new List<FaceDetection> { face.Detection }, face.Encoding));
                newClustersCount++;
                _logger.LogDebug($"Created new cluster for face {face.Detection.FaceDetectionId} (best similarity was {bestSimilarity:F3}, threshold: {similarityThreshold})");
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

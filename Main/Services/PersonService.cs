using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services;

public class PersonService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PersonService> _logger;
    private readonly FaceDetectionService _faceDetectionService;

    public PersonService(ApplicationDbContext context, ILogger<PersonService> logger, FaceDetectionService faceDetectionService)
    {
        _context = context;
        _logger = logger;
        _faceDetectionService = faceDetectionService;
    }

    // ... existing methods ...

    public async Task<byte[]> GetPersonPreviewAsync(int personId)
    {
        var person = await _context.Persons
            .Include(p => p.FaceDetections)
                .ThenInclude(fd => fd.Shot)
            .FirstOrDefaultAsync(p => p.PersonId == personId);

        if (person == null) return null;

        // If ProfilePhotoId is set, use that shot
        if (person.ProfilePhotoId.HasValue)
        {
            var profileFace = person.FaceDetections
                .FirstOrDefault(fd => fd.ShotId == person.ProfilePhotoId.Value);

            if (profileFace != null)
            {
                var image = await _faceDetectionService.GetFaceImageAsync(profileFace.FaceDetectionId);
                if (image != null)
                {
                    // Update cached preview
                    person.Preview = image;
                    await _context.SaveChangesAsync();
                    return image;
                }
            }
        }

        // Return cached preview if available (and ProfilePhotoId is not set)
        if (person.Preview != null && person.Preview.Length > 0)
        {
            return person.Preview;
        }

        // Generate preview from first face (fallback)
        var face = person.FaceDetections.OrderBy(fd => fd.DetectedAt).FirstOrDefault();
        if (face != null)
        {
            var image = await _faceDetectionService.GetFaceImageAsync(face.FaceDetectionId);
            if (image != null)
            {
                person.Preview = image;
                await _context.SaveChangesAsync();
                return image;
            }
        }

        return null;
    }


    public async Task<Person> CreatePersonAsync(string firstName, string lastName, int userId)
    {
        // Note: Person table currently doesn't have UserId, so persons are global or shared?
        // Looking at the schema, Person doesn't have UserId. 
        // But the user request implies "my people".
        // Existing Person entity:
        // public class Person { PersonId, FirstName, LastName, Shots }
        // It seems Persons are shared across the system or just one user system?
        // The README says "keep persons database".
        // If it's a single user system (or small family), global persons might be fine.
        // But if we want per-user persons, we should have added UserId to Person.
        // The implementation plan didn't explicitly add UserId to Person.
        // I'll proceed with global persons for now as per existing schema, 
        // but we might want to link them to User later if needed.

        var person = new Person
        {
            FirstName = firstName,
            LastName = lastName
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync();
        return person;
    }

    public async Task ConfirmFaceAssignmentAsync(int faceDetectionId)
    {
        var detection = await _context.FaceDetections.FindAsync(faceDetectionId);
        if (detection != null)
        {
            detection.IsConfirmed = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> ConfirmFacesAsync(List<int> faceDetectionIds)
    {
        var detections = await _context.FaceDetections
            .Where(f => faceDetectionIds.Contains(f.FaceDetectionId))
            .ToListAsync();

        foreach (var detection in detections)
        {
            detection.IsConfirmed = true;
        }

        await _context.SaveChangesAsync();
        return detections.Count;
    }

    public async Task ReassignFaceAsync(int faceDetectionId, int newPersonId)
    {
        var detection = await _context.FaceDetections.FindAsync(faceDetectionId);
        if (detection != null)
        {
            detection.PersonId = newPersonId;
            detection.IsConfirmed = true; // Manual assignment implies confirmation
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteFaceAsync(int faceDetectionId)
    {
        var detection = await _context.FaceDetections.FindAsync(faceDetectionId);
        if (detection != null)
        {
            // Delete associated face encoding first
            var encoding = await _context.FaceEncodings
                .FirstOrDefaultAsync(e => e.FaceDetectionId == faceDetectionId);
            if (encoding != null)
            {
                _context.FaceEncodings.Remove(encoding);
            }

            // Delete the face detection
            _context.FaceDetections.Remove(detection);
            await _context.SaveChangesAsync();
        }
    }

    public async Task MergePeopleAsync(int fromPersonId, int toPersonId)
    {
        var fromPerson = await _context.Persons
            .Include(p => p.FaceDetections)
            .FirstOrDefaultAsync(p => p.PersonId == fromPersonId);

        var toPerson = await _context.Persons.FindAsync(toPersonId);

        if (fromPerson == null || toPerson == null)
        {
            return;
        }

        // Move all face detections
        foreach (var detection in fromPerson.FaceDetections)
        {
            detection.PersonId = toPersonId;
            // Keep confirmation status or reset? 
            // If we merge, we probably assume the user knows what they are doing, so keep as is or confirm.
        }

        // Move all shots (many-to-many) - this is trickier with EF Core if not explicitly loaded
        // We need to handle the PersonShot join table.
        // Since we have FaceDetections now, the PersonShot table might be redundant or derived?
        // The existing system uses PersonShot. We should probably maintain it for backward compatibility
        // or sync it with FaceDetections.
        // For now, let's just update FaceDetections as that's the new source of truth for "this person is in this photo".

        _context.Persons.Remove(fromPerson);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Shot>> GetShotsForPersonAsync(int personId)
    {
        // Return shots linked via FaceDetections
        return await _context.FaceDetections
            .Where(fd => fd.PersonId == personId)
            .Select(fd => new Shot
            {
                ShotId = fd.Shot.ShotId,
                Flip = fd.Shot.Flip,
                Rotate = fd.Shot.Rotate
            })
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<Person>> GetAllPersonsAsync()
    {
        return await _context.Persons
            .Include(p => p.FaceDetections)
            .OrderByDescending(p => p.FaceDetections.Count)
            .ThenBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToListAsync();
    }

    public async Task SetProfilePhotoAsync(int personId, int shotId)
    {
        _logger.LogInformation($"[SetProfilePhotoAsync] Setting profile photo for person {personId} to shot {shotId}");
        var person = await _context.Persons.FindAsync(personId);
        if (person != null)
        {
            _logger.LogInformation($"[SetProfilePhotoAsync] Found person {personId}, current ProfilePhotoId: {person.ProfilePhotoId}");
            person.ProfilePhotoId = shotId;
            _logger.LogInformation($"[SetProfilePhotoAsync] Updated ProfilePhotoId to: {person.ProfilePhotoId}");
            var changes = await _context.SaveChangesAsync();
            _logger.LogInformation($"[SetProfilePhotoAsync] SaveChanges returned {changes} modified entities");
        }
        else
        {
            _logger.LogWarning($"[SetProfilePhotoAsync] Person {personId} not found in context");
        }
    }

    /// <summary>
    /// Calculates quality metrics for a person's cluster
    /// </summary>
    public async Task<PersonQualityMetrics> GetPersonQualityMetricsAsync(int personId, int maxSampleSize = 50)
    {
        // Optimized query: only load FaceDetectionId and Encoding bytes, skip full entity loading
        var encodingsQuery = _context.FaceEncodings
            .AsNoTracking()
            .Where(fe => fe.FaceDetection.PersonId == personId);

        var totalCount = await encodingsQuery.CountAsync();

        if (totalCount == 0)
        {
            return null;
        }

        // Sample if too many faces (for performance)
        List<FaceEncodingDto> encodingData;
        if (totalCount > maxSampleSize)
        {
            // Take evenly distributed sample by selecting every Nth face
            var allIds = await encodingsQuery.Select(e => e.FaceDetectionId).ToListAsync();
            var step = Math.Max(1, totalCount / maxSampleSize);
            var sampledIds = allIds.Where((id, index) => index % step == 0).Take(maxSampleSize).ToHashSet();

            encodingData = await encodingsQuery
                .Where(e => sampledIds.Contains(e.FaceDetectionId))
                .Select(fe => new FaceEncodingDto { FaceDetectionId = fe.FaceDetectionId, Encoding = fe.Encoding })
                .ToListAsync();
        }
        else
        {
            encodingData = await encodingsQuery
                .Select(fe => new FaceEncodingDto { FaceDetectionId = fe.FaceDetectionId, Encoding = fe.Encoding })
                .ToListAsync();
        }

        var encodings = encodingData
            .Select(e => new
            {
                FaceDetectionId = e.FaceDetectionId,
                Encoding = ByteArrayToFloatArray(e.Encoding)
            })
            .ToList();

        if (encodings.Count < 2)
        {
            return new PersonQualityMetrics
            {
                PersonId = personId,
                FaceCount = encodings.Count,
                MinPairwiseSimilarity = 1.0f,
                AvgPairwiseSimilarity = 1.0f,
                Dispersion = 0.0f,
                StdDevPairwise = 0.0f,
                OutlierFaceIds = new List<int>()
            };
        }

        // Calculate centroid
        var centroid = CalculateCentroid(encodings.Select(e => e.Encoding).ToList());

        // Calculate all pairwise similarities
        var pairwiseSimilarities = new List<float>();
        for (int i = 0; i < encodings.Count; i++)
        {
            for (int j = i + 1; j < encodings.Count; j++)
            {
                pairwiseSimilarities.Add(CosineSimilarity(encodings[i].Encoding, encodings[j].Encoding));
            }
        }

        // Calculate dispersion (average distance from centroid)
        float sumDist = 0;
        var distancesFromCentroid = new Dictionary<int, float>();
        foreach (var enc in encodings)
        {
            float similarity = CosineSimilarity(enc.Encoding, centroid);
            float dist = 1 - similarity;
            sumDist += dist;
            distancesFromCentroid[enc.FaceDetectionId] = dist;
        }
        float dispersion = sumDist / encodings.Count;

        // Calculate standard deviation
        float avgPairwise = pairwiseSimilarities.Average();
        float sumSquaredDiff = pairwiseSimilarities.Sum(v => (v - avgPairwise) * (v - avgPairwise));
        float stdDev = (float)Math.Sqrt(sumSquaredDiff / pairwiseSimilarities.Count);

        // Identify outliers (faces far from centroid)
        // Outlier threshold: distance > mean + 1.5 * std_dev
        float avgDistance = distancesFromCentroid.Values.Average();
        float distStdDev = (float)Math.Sqrt(distancesFromCentroid.Values.Sum(d => (d - avgDistance) * (d - avgDistance)) / distancesFromCentroid.Count);
        float outlierThreshold = avgDistance + 1.5f * distStdDev;

        var outlierFaceIds = distancesFromCentroid
            .Where(kvp => kvp.Value > outlierThreshold)
            .Select(kvp => kvp.Key)
            .ToList();

        return new PersonQualityMetrics
        {
            PersonId = personId,
            FaceCount = encodings.Count,
            MinPairwiseSimilarity = pairwiseSimilarities.Min(),
            AvgPairwiseSimilarity = avgPairwise,
            Dispersion = dispersion,
            StdDevPairwise = stdDev,
            OutlierFaceIds = outlierFaceIds
        };
    }

    private float[] ByteArrayToFloatArray(byte[] bytes)
    {
        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private float[] CalculateCentroid(List<float[]> vectors)
    {
        if (!vectors.Any()) return null;

        int dim = vectors[0].Length;
        var centroid = new float[dim];
        foreach (var v in vectors)
        {
            for (int i = 0; i < dim; i++) centroid[i] += v[i];
        }

        for (int i = 0; i < dim; i++) centroid[i] /= vectors.Count;
        return centroid;
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
}

public class PersonQualityMetrics
{
    public int PersonId { get; set; }
    public int FaceCount { get; set; }
    public float MinPairwiseSimilarity { get; set; }
    public float AvgPairwiseSimilarity { get; set; }
    public float Dispersion { get; set; }
    public float StdDevPairwise { get; set; }
    public List<int> OutlierFaceIds { get; set; }
}

internal class FaceEncodingDto
{
    public int FaceDetectionId { get; set; }
    public byte[] Encoding { get; set; }
}

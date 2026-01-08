using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Data;

class FaceDetectionDiagnostic
{
    static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        optionsBuilder.UseNpgsql(connectionString);

        using var dbContext = new ApplicationDbContext(optionsBuilder.Options);

        int personId = args.Length > 0 ? int.Parse(args[0]) : 1174;

        Console.WriteLine($"=== Diagnostic for Person #{personId} ===\n");

        // Get person info
        var person = dbContext.Persons
            .Include(p => p.FaceDetections)
                .ThenInclude(fd => fd.Shot)
            .FirstOrDefault(p => p.PersonId == personId);

        if (person == null)
        {
            Console.WriteLine($"Person #{personId} not found!");
            return;
        }

        Console.WriteLine($"Person: {person.FirstName} {person.LastName}");
        Console.WriteLine($"Total face detections: {person.FaceDetections.Count}\n");

        // Group by shot
        var facesByShot = person.FaceDetections
            .GroupBy(fd => fd.ShotId)
            .OrderBy(g => g.Key);

        foreach (var shotGroup in facesByShot)
        {
            var shot = shotGroup.First().Shot;
            Console.WriteLine($"Shot #{shotGroup.Key} (Album #{shot?.AlbumId}):");
            foreach (var face in shotGroup.OrderBy(f => f.FaceDetectionId))
            {
                Console.WriteLine($"  Face #{face.FaceDetectionId}: " +
                    $"Position=({face.X},{face.Y}) Size=({face.Width}x{face.Height}) " +
                    $"Confirmed={face.IsConfirmed} DetectedAt={face.DetectedAt:yyyy-MM-dd HH:mm:ss}");
            }
            Console.WriteLine();
        }

        // Get recent face detections
        Console.WriteLine("\n=== Recent Face Detections (All People, Last 20) ===\n");
        var recentFaces = dbContext.FaceDetections
            .Include(fd => fd.Shot)
            .Include(fd => fd.Person)
            .OrderByDescending(fd => fd.DetectedAt)
            .Take(20)
            .ToList();

        foreach (var face in recentFaces)
        {
            var personName = face.Person != null
                ? $"{face.Person.FirstName} {face.Person.LastName} (#{face.PersonId})"
                : "Unassigned";
            Console.WriteLine($"Face #{face.FaceDetectionId}: Shot #{face.ShotId} " +
                $"-> {personName} | Detected: {face.DetectedAt:yyyy-MM-dd HH:mm:ss}");
        }

        // Check for faces without person assignment
        Console.WriteLine("\n=== Unassigned Faces (No PersonId) ===\n");
        var unassignedCount = dbContext.FaceDetections.Count(fd => fd.PersonId == null);
        Console.WriteLine($"Total unassigned faces: {unassignedCount}");

        if (unassignedCount > 0)
        {
            var recentUnassigned = dbContext.FaceDetections
                .Where(fd => fd.PersonId == null)
                .OrderByDescending(fd => fd.DetectedAt)
                .Take(10)
                .ToList();

            foreach (var face in recentUnassigned)
            {
                Console.WriteLine($"  Face #{face.FaceDetectionId}: Shot #{face.ShotId} " +
                    $"Detected: {face.DetectedAt:yyyy-MM-dd HH:mm:ss}");
            }
        }

        // Check total persons and their face counts
        Console.WriteLine("\n=== People Summary (Top 10 by Face Count) ===\n");
        var peopleStats = dbContext.Persons
            .Select(p => new {
                PersonId = p.PersonId,
                Name = $"{p.FirstName} {p.LastName}",
                FaceCount = p.FaceDetections.Count
            })
            .Where(p => p.FaceCount > 0)
            .OrderByDescending(p => p.FaceCount)
            .Take(10)
            .ToList();

        foreach (var stat in peopleStats)
        {
            Console.WriteLine($"Person #{stat.PersonId}: {stat.Name} - {stat.FaceCount} faces");
        }
    }
}

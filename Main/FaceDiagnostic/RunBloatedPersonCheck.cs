using System;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services;

namespace FaceDiagnostic;

/// <summary>
/// Console tool to analyze database and find bloated persons
/// </summary>
public class RunBloatedPersonCheck
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("Bloated Person Detection - Database Scan");
        Console.WriteLine("===========================================\n");

        // Setup configuration - look in parent directory for appsettings.json
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var parentDir = System.IO.Directory.GetParent(baseDir)?.Parent?.Parent?.Parent?.FullName ?? baseDir;
        var configPath = System.IO.Path.Combine(parentDir, "appsettings.json");

        if (!System.IO.File.Exists(configPath))
        {
            // Fallback to current directory
            configPath = System.IO.Path.Combine(baseDir, "appsettings.json");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(System.IO.Path.GetDirectoryName(configPath) ?? baseDir)
            .AddJsonFile(System.IO.Path.GetFileName(configPath), optional: false)
            .Build();

        // Setup DbContext
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        optionsBuilder.UseNpgsql(connectionString);

        using var dbContext = new ApplicationDbContext(optionsBuilder.Options);

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<FaceClusteringService>();

        // Create service
        var clusteringService = new FaceClusteringService(dbContext, logger);

        try
        {
            // Get all users
            var users = await dbContext.Users.ToListAsync();

            if (!users.Any())
            {
                Console.WriteLine("No users found in database.");
                return;
            }

            Console.WriteLine($"Found {users.Count} user(s). Analyzing persons...\n");

            foreach (var user in users)
            {
                Console.WriteLine($"Analyzing persons for user: {user.Username} (ID: {user.UserId})");
                Console.WriteLine(new string('-', 80));

                var bloatedPersons = await clusteringService.FindBloatedPersonsAsync(user.UserId);

                if (!bloatedPersons.Any())
                {
                    Console.WriteLine("✓ No bloated persons found for this user!\n");
                    continue;
                }

                Console.WriteLine($"⚠ Found {bloatedPersons.Count} bloated person(s):\n");

                // Print table header
                Console.WriteLine(String.Format(
                    "{0,-4} {1,-8} {2,-25} {3,-6} {4,-8} {5,-8} {6,-10} {7,-8}",
                    "Sev", "ID", "Name", "Faces", "MinSim", "AvgSim", "Dispersion", "StdDev"
                ));
                Console.WriteLine(new string('-', 80));

                foreach (var person in bloatedPersons)
                {
                    var severityColor = person.Severity switch
                    {
                        "CRITICAL" => ConsoleColor.Red,
                        "HIGH" => ConsoleColor.Yellow,
                        "MEDIUM" => ConsoleColor.Cyan,
                        _ => ConsoleColor.White
                    };

                    Console.ForegroundColor = severityColor;
                    Console.WriteLine(String.Format(
                        "{0,-4} {1,-8} {2,-25} {3,-6} {4,-8:F3} {5,-8:F3} {6,-10:F3} {7,-8:F3}",
                        person.Severity,
                        person.PersonId,
                        $"{person.FirstName} {person.LastName}",
                        person.FaceCount,
                        person.MinPairwiseSimilarity,
                        person.AvgPairwiseSimilarity,
                        person.Dispersion,
                        person.StdDevPairwise
                    ));
                    Console.ResetColor();
                }

                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"\nSummary by Severity:");
                var criticalCount = bloatedPersons.Count(p => p.Severity == "CRITICAL");
                var highCount = bloatedPersons.Count(p => p.Severity == "HIGH");
                var mediumCount = bloatedPersons.Count(p => p.Severity == "MEDIUM");

                if (criticalCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  CRITICAL: {criticalCount} (definitely bloated, unrelated faces)");
                    Console.ResetColor();
                }
                if (highCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  HIGH:     {highCount} (very likely bloated)");
                    Console.ResetColor();
                }
                if (mediumCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  MEDIUM:   {mediumCount} (probably bloated)");
                    Console.ResetColor();
                }

                Console.WriteLine("\nRecommendation: Review these persons at /persons/bloated");
                Console.WriteLine("Delete incorrect persons - they won't reappear due to quality checks.\n");
            }

            Console.WriteLine("===========================================");
            Console.WriteLine("Analysis Complete");
            Console.WriteLine("===========================================");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
            Console.ResetColor();
        }
    }
}

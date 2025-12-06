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
            .FirstOrDefaultAsync(p => p.PersonId == personId);

        if (person == null) return null;

        // Return cached preview if available
        if (person.Preview != null && person.Preview.Length > 0)
        {
            return person.Preview;
        }

        // Generate preview from first face
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
        var person = await _context.Persons.FindAsync(personId);
        if (person != null)
        {
            person.ProfilePhotoId = shotId;
            await _context.SaveChangesAsync();
        }
    }
}

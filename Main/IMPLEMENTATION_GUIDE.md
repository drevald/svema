# Face Clustering Settings Implementation Guide

## Files Created:
1. ✅ `Models/ClusteringSettings.cs` - Settings model
2. ✅ `Services/ClusteringSettingsService.cs` - Settings service
3. ✅ `add_clustering_settings.sql` - SQL migration (DEPRECATED - use EF migration instead)

## Changes Needed:

### 1. Update `Data/ApplicationDbContext.cs`

Add this line after line 69 (after `FaceEncodings`):
```csharp
public DbSet<ClusteringSettings> ClusteringSettings { get; set; }
```

### 2. Update `Services/FaceClusteringService.cs`

**Add helper method at the end of the class (before closing brace):**
```csharp
private async Task<ClusteringSettings> GetOrCreateSettingsAsync(int userId)
{
    var settings = await _context.ClusteringSettings
        .FirstOrDefaultAsync(s => s.UserId == userId);

    if (settings == null)
    {
        // Create default settings
        settings = new ClusteringSettings
        {
            UserId = userId,
            Preset = ClusteringPreset.Balanced,
            SimilarityThreshold = 0.23f,
            MinFacesPerPerson = 2,
            MinFaceSize = 80,
            MinFaceQuality = 0.3f,
            AutoMergeThreshold = 0.80f,
            IsFaceProcessingSuspended = false
        };
        _context.ClusteringSettings.Add(settings);
        await _context.SaveChangesAsync();
    }

    return settings;
}
```

**Replace `ClusterUnassignedFacesAsync` method first lines (up to line 67):**

FIND:
```csharp
public async Task<int> ClusterUnassignedFacesAsync(int userId)
{
    // 1. Get unassigned faces
    var unassignedFaces = await _context.FaceDetections
```

REPLACE WITH:
```csharp
public async Task<int> ClusterUnassignedFacesAsync(int userId)
{
    // Load user's clustering settings
    var settings = await GetOrCreateSettingsAsync(userId);

    // Check if processing is suspended
    if (settings.IsFaceProcessingSuspended)
    {
        _logger.LogInformation($"Face processing is suspended for user {userId}");
        return 0;
    }

    // 1. Get unassigned faces
    var unassignedFaces = await _context.FaceDetections
```

**Update line 34 logging:**

FIND:
```csharp
_logger.LogInformation($"Starting clustering for {unassignedFaces.Count} unassigned faces.");
```

REPLACE WITH:
```csharp
_logger.LogInformation($"Starting clustering for {unassignedFaces.Count} unassigned faces (Preset: {settings.Preset}, Threshold: {settings.SimilarityThreshold})");
```

**Update face data preparation (around line 60-67):**

FIND:
```csharp
// 3. Prepare unassigned faces
var faceData = unassignedFaces.Select(fd => new
{
    Detection = fd,
    Encoding = ByteArrayToFloatArray(fd.FaceEncoding.Encoding)
}).ToList();

const float similarityThreshold = 0.23f; // Very lenient threshold for maximum face grouping
int newClustersCount = 0;
```

REPLACE WITH:
```csharp
// 3. Prepare unassigned faces (apply quality and size filters)
var faceData = unassignedFaces.Select(fd => new
{
    Detection = fd,
    Encoding = ByteArrayToFloatArray(fd.FaceEncoding.Encoding)
})
.Where(f => f.Detection.Width >= settings.MinFaceSize && f.Detection.Height >= settings.MinFaceSize)
.ToList();

_logger.LogInformation($"After size filter ({settings.MinFaceSize}px): {faceData.Count} faces remaining");

float similarityThreshold = settings.SimilarityThreshold;
int minFacesPerPerson = settings.MinFacesPerPerson;
int newClustersCount = 0;
```

**Update cluster creation logic (around line 165-169):**

FIND:
```csharp
else
{
    // Create new cluster
    newClusters.Add((new List<FaceDetection> { face.Detection }, new List<float[]> { face.Encoding }));
    newClustersCount++;
    _logger.LogInformation($"Created new cluster for face {face.Detection.FaceDetectionId} (best similarity was {bestScore:F3}, threshold: {similarityThreshold})");
}
```

REPLACE WITH:
```csharp
else
{
    // Create new cluster (will only become person if >= minFacesPerPerson)
    newClusters.Add((new List<FaceDetection> { face.Detection }, new List<float[]> { face.Encoding }));
    newClustersCount++;
    _logger.LogInformation($"Created new cluster for face {face.Detection.FaceDetectionId} (best similarity was {bestScore:F3}, threshold: {similarityThreshold})");
}
```

**Update person creation logic (around line 177-196):**

FIND:
```csharp
// 5. Create new persons for new clusters
foreach (var cluster in newClusters)
{
    var person = new Person
```

REPLACE WITH:
```csharp
// 5. Create new persons for new clusters (only if >= minFacesPerPerson)
var validClusters = newClusters.Where(c => c.Faces.Count >= minFacesPerPerson).ToList();
var skippedFaces = newClusters.Where(c => c.Faces.Count < minFacesPerPerson).Sum(c => c.Faces.Count);

if (skippedFaces > 0)
{
    _logger.LogInformation($"Skipped creating persons for {skippedFaces} faces (< {minFacesPerPerson} faces per cluster)");
}

foreach (var cluster in validClusters)
{
    var person = new Person
```

### 3. Update `Controllers/MainController.cs`

**Add at the end, before the closing brace (after PersonsList method):**

```csharp
[Authorize]
[HttpGet("settings")]
public async Task<IActionResult> Settings()
{
    var userId = HttpContext.Session.GetInt32("UserId");
    if (!userId.HasValue) return Unauthorized();

    var settingsService = new ClusteringSettingsService(dbContext);
    var settings = await settingsService.GetOrCreateSettingsAsync(userId.Value);

    return View(settings);
}

[Authorize]
[HttpPost("settings/update")]
public async Task<IActionResult> UpdateSettings([FromForm] string preset, [FromForm] float? threshold, [FromForm] int? minFaces, [FromForm] int? minSize, [FromForm] float? minQuality, [FromForm] float? autoMerge)
{
    var userId = HttpContext.Session.GetInt32("UserId");
    if (!userId.HasValue) return Unauthorized();

    var settingsService = new ClusteringSettingsService(dbContext);
    var presetEnum = Enum.Parse<ClusteringPreset>(preset);

    await settingsService.UpdateSettingsAsync(userId.Value, presetEnum, threshold, minFaces, minSize, minQuality, autoMerge);

    return RedirectToAction("Settings");
}

[Authorize]
[HttpPost("settings/toggle-processing")]
public async Task<IActionResult> ToggleProcessing()
{
    var userId = HttpContext.Session.GetInt32("UserId");
    if (!userId.HasValue) return Unauthorized();

    var settingsService = new ClusteringSettingsService(dbContext);
    var isSuspended = await settingsService.ToggleProcessingSuspendedAsync(userId.Value);

    return Json(new { suspended = isSuspended });
}

[Authorize]
[HttpPost("settings/delete-all-faces")]
public async Task<IActionResult> DeleteAllFaces()
{
    var userId = HttpContext.Session.GetInt32("UserId");
    if (!userId.HasValue) return Unauthorized();

    // Delete all face data for this user
    var userAlbums = await dbContext.Albums
        .Where(a => a.UserId == userId.Value)
        .Select(a => a.Id)
        .ToListAsync();

    var shotIds = await dbContext.Shots
        .Where(s => userAlbums.Contains(s.AlbumId))
        .Select(s => s.Id)
        .ToListAsync();

    var faceDetectionIds = await dbContext.FaceDetections
        .Where(fd => shotIds.Contains(fd.ShotId))
        .Select(fd => fd.FaceDetectionId)
        .ToListAsync();

    // Delete encodings
    var encodings = await dbContext.FaceEncodings
        .Where(fe => faceDetectionIds.Contains(fe.FaceDetectionId))
        .ToListAsync();
    dbContext.FaceEncodings.RemoveRange(encodings);

    // Delete detections
    var detections = await dbContext.FaceDetections
        .Where(fd => shotIds.Contains(fd.ShotId))
        .ToListAsync();
    dbContext.FaceDetections.RemoveRange(detections);

    // Delete persons
    var personIds = detections.Where(fd => fd.PersonId.HasValue).Select(fd => fd.PersonId.Value).Distinct().ToList();
    var persons = await dbContext.Persons
        .Where(p => personIds.Contains(p.PersonId))
        .ToListAsync();
    dbContext.Persons.RemoveRange(persons);

    // Reset face processing flag
    var shots = await dbContext.Shots
        .Where(s => shotIds.Contains(s.Id))
        .ToListAsync();
    foreach (var shot in shots)
    {
        shot.IsFaceProcessed = false;
    }

    await dbContext.SaveChangesAsync();

    return Json(new { success = true, message = $"Deleted {detections.Count} face detections, {encodings.Count} encodings, and {persons.Count} persons. Shots reset for reprocessing." });
}

[Authorize]
[HttpPost("person/{personId}/remove-face/{faceId}")]
public async Task<IActionResult> RemoveFaceFromPerson(int personId, int faceId)
{
    var face = await dbContext.FaceDetections.FindAsync(faceId);
    if (face == null || face.PersonId != personId) return NotFound();

    face.PersonId = null;
    face.IsConfirmed = false;

    await dbContext.SaveChangesAsync();

    return Json(new { success = true, message = "Face removed. Centroid will update on next clustering." });
}
```

### 4. Create Migration

```bash
cd "C:/Projects/svema/Main"
dotnet ef migrations add AddClusteringSettings
dotnet ef database update
```

### 5. Create `Views/Main/Settings.cshtml`

See separate file `Settings.cshtml`

### 6. Rebuild and Redeploy

```bash
cd "C:/Projects/svema/Main"
docker compose down
docker compose up --build --force-recreate -d
```

## Testing:

1. Navigate to `/settings`
2. Try changing presets
3. Toggle suspend/resume
4. Delete all faces
5. Remove a face from a person

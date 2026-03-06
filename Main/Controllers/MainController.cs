using System;
using System.Collections.Generic;
using System.Globalization;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Form;
using Data;
using Models;
using Utils;
using Common;
using Services;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace Svema.Controllers;

public class MainController(
    ApplicationDbContext dbContext,
    IConfiguration config,
    FaceDetectionService faceDetectionService,
    FaceClusteringService faceClusteringService,
    PersonService personService,
    ILogger<MainController> logger) : BaseController(dbContext, config)
{

    public List<string> GetCameraModels()
    {
        return albumService.GetCameraModels();
    }

    [Authorize]
    [HttpGet("")]
    public IActionResult Albums(AlbumsListDTO dto)
    {
        dto ??= new();
        var model = BuildAlbumsListAsync(dto, onlyMine: false);
        return View(model);
    }

    [Authorize]
    [HttpPost("")]
    public IActionResult ReloadAlbums(AlbumsListDTO dto)
    {
        dto ??= new();
        var model = BuildAlbumsListAsync(dto, onlyMine: false);
        return View("Albums", model);
    }

    [Authorize]
    [HttpPost("random_shots")]
    public IActionResult GetRandomShots(AlbumsListDTO dto)
    {
        dto ??= new();
        var username = GetUsername() ?? string.Empty;
        var query = albumService.ApplyShotFilters(dto, username, onlyMine: false);

        // Optimization: Fetch only IDs first to avoid expensive ORDER BY RANDOM() in DB
        var allIds = query.Select(s => s.ShotId).ToList();

        if (allIds.Count == 0)
        {
            return Json(new List<ShotPreviewDTO>());
        }

        List<int> selectedIds;
        if (allIds.Count <= 100)
        {
            selectedIds = allIds;
        }
        else
        {
            // Pick 100 random indices to avoid sorting a potentially large list
            var rnd = new Random();
            var indices = new HashSet<int>();
            while (indices.Count < 100)
            {
                indices.Add(rnd.Next(allIds.Count));
            }
            selectedIds = [.. indices.Select(i => allIds[i])];
        }

        var shots = dbContext.Shots
            .Where(s => selectedIds.Contains(s.ShotId))
            .Select(s => new ShotPreviewDTO
            {
                ShotId = s.ShotId,
                Name = s.Name,
                SourceUri = s.SourceUri,
                Flip = s.Flip,
                Rotate = s.Rotate,
                DateStart = s.DateStart
            })
            .ToList();

        // Shuffle the final small list for display order
        var shuffledShots = shots.OrderBy(x => Guid.NewGuid()).ToList();

        return Json(shuffledShots);
    }

    [Authorize]
    [HttpGet("my")]
    public IActionResult MyAlbums(AlbumsListDTO dto)
    {
        dto ??= new();
        var model = BuildAlbumsListAsync(dto, onlyMine: true);
        return View(model);
    }

    [Authorize]
    [HttpPost("my")]
#nullable enable
    public async Task<IActionResult> UpdateMyAlbums(AlbumsListDTO dto, string? delete, string? save)
    {
        if (dto == null || dto.Albums == null)
        {
            return RedirectToAction("MyAlbums");
        }

        if (!string.IsNullOrEmpty(dto.LocationName) && save != null)
        {
            locationService.AddLocationIfProvided(dto.LocationName, dto.Latitude, dto.Longitude, dto.Zoom);
        }

        foreach (var a in dto.Albums)
        {
            if (a == null) continue;
            if (a.IsChecked)
            {
                var shotsToChange = shotService.GetShotIdsByAlbum(a.AlbumId);
                if (save != null)
                {
                    if (dto.EditLocation && shotsToChange.Count > 0)
                    {
                        shotService.BulkUpdateShotsLocation(shotsToChange, dto.Latitude, dto.Longitude, dto.Zoom);
                    }
                    dbContext.SaveChanges();
                }

                if (delete != null)
                {
                    await DeleteAlbum(a.AlbumId);
                }
            }
        }

        // return View("MyAlbums", model);

        return RedirectToAction("MyAlbums", new
        {
            dto.SortBy,
            dto.SortDirection,
            dto.DateStart,
            dto.DateEnd,
            dto.Camera,
            dto.LocationId,
            dto.Latitude,
            dto.Longitude,
            dto.Zoom,
            dto.EditLocation,
            dto.North,
            dto.South,
            dto.East,
            dto.West
        });

    }

    protected AlbumsListDTO BuildAlbumsListAsync(AlbumsListDTO dto, bool onlyMine)
    {
        var username = GetUsername() ?? string.Empty;
        var result = albumService.BuildAlbumsListAsync(dto, username, onlyMine);

        // Use server-side PostGIS clustering for large datasets
        var placemarks = locationService.GetClusteredShotsWithLabels(username, onlyMine, dto.West, dto.East, dto.South, dto.North);
        var rect = GeoRect.FromPlacemarks(placemarks, 0.1);
        result.North = rect.North;
        result.South = rect.South;
        result.West = rect.West;
        result.East = rect.East;
        result.Placemarks = placemarks;

        return result;
    }

    public IQueryable<Shot> ApplyShotFilters(AlbumsListDTO dto, bool onlyMine)
    {
        var username = GetUsername() ?? string.Empty;
        return albumService.ApplyShotFilters(dto, username, onlyMine);
    }

    ///////////////////   ALBUM  /////////////////////////////////////////

    [Authorize]
    [HttpGet("edit_album")]
    public IActionResult EditAlbum(int id, double? north, double? south, double? west, double? east)
    {
        AlbumDTO dto = new();
        dto.North = north ?? dto.North;
        dto.South = south ?? dto.South;
        dto.East = east ?? dto.East;
        dto.West = west ?? dto.West;

        var currentUserId = GetUserId();

        var album = albumService.GetAlbumWithUser(id, currentUserId!.Value);

        if (album == null)
        {
            return RedirectToAction("Albums");
        }

        dto.Shots = shotService.GetShotPreviews(id, dto.West, dto.East, dto.South, dto.North);

        dto.Placemarks = locationService.GetShotsForShots(dto.Shots);
        GeoRect rect = GeoRect.FromPlacemarks(dto.Placemarks, 0.1);
        dto.North = north ?? rect.North;
        dto.South = south ?? rect.South;
        dto.East = east ?? rect.East;
        dto.West = west ?? rect.West;
        dto.AlbumId = album.AlbumId;
        dto.Name = album.Name;
        dto.AlbumComments = album.AlbumComments ?? [];
        dto.Locations = locationService.GetLocations();

        return View(dto);
    }

    [Authorize]
    [HttpPost("edit_album")]
    public async Task<IActionResult> StoreAlbum(AlbumDTO dto, string? refresh)
    {

        if (refresh != null)
        {
            return RedirectToAction("EditAlbum", new
            {
                Id = dto.AlbumId,
                dto.North,
                dto.South,
                dto.West,
                dto.East
            });
        }

        if (dto == null)
        {
            Console.WriteLine("DTO is null");
            return BadRequest();
        }

        var storedAlbum = albumService.GetAlbum(dto.AlbumId);
        if (storedAlbum == null)
        {
            Console.WriteLine($"Album with id {dto.AlbumId} not found");
            return NotFound();
        }

        if (dto.Shots == null)
        {
            Console.WriteLine($"DTO.Shots is null for album {dto.AlbumId}");
            return BadRequest();
        }

        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] START >>>>>> ");

        // Update album name
        albumService.UpdateAlbumName(dto.AlbumId, dto.Name);

        // Get shots that need updates
        var shotsToUpdate = dto.Shots
            .Where(s => s != null && (s.IsChecked || s.Rotate != 0 || s.Flip))
            .Select(s => s.ShotId)
            .Where(id => id > 0)
            .ToList();

        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] FOUND {shotsToUpdate.Count} SHOTS TO UPDATE");

        if (shotsToUpdate.Count > 0)
        {
            shotService.BulkUpdateShots(shotsToUpdate, dto);
        }

        // Add location if specified
        locationService.AddLocationIfProvided(dto.LocationName, dto.Latitude, dto.Longitude, dto.Zoom);

        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] START SAVE CHANGES");
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] END SAVE CHANGES <<<<<<<<<<<<< ");

        return Redirect("/my");
    }

    [Authorize]
    [HttpGet("add_album")]
    public IActionResult AddAlbum()
    {
        return View();
    }

    [Authorize]
    [HttpPost("add_album")]
    public IActionResult CreateAlbum(Album album)
    {
        if (album == null) return BadRequest();

        Console.Write("STORING ALBUM (" + album.AlbumId + ")");
        var user = userService.GetUserByUsername(GetUsername());
        if (user == null)
        {
            return Unauthorized();
        }
        album.User = user;
        albumService.CreateAlbum(album);
        return Redirect("/");
    }

    [Authorize]
    [HttpGet("delete_album")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        await albumService.DeleteAlbumAsync(id);
        return Redirect("/my");
    }

    [Authorize]
    [HttpGet("detect_persons_album")]
    public async Task<IActionResult> DetectPersonsInAlbum(int id)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var album = albumService.GetAuthorizedAlbum(id, userId.Value, null);
        if (album == null) return NotFound();

        // Get all shots in this album and reset their face detection flags
        var shots = await dbContext.Shots
            .Where(s => s.AlbumId == id)
            .ToListAsync();

        // Reset flags and clear existing face data for re-detection
        var shotIds = shots.Select(s => s.ShotId).ToList();

        // Delete existing face encodings and detections for these shots
        var faceDetections = await dbContext.FaceDetections
            .Where(fd => shotIds.Contains(fd.ShotId))
            .Include(fd => fd.FaceEncoding)
            .ToListAsync();

        if (faceDetections.Any())
        {
            var faceEncodings = faceDetections
                .Where(fd => fd.FaceEncoding != null)
                .Select(fd => fd.FaceEncoding!)
                .ToList();

            dbContext.FaceEncodings.RemoveRange(faceEncodings);
            dbContext.FaceDetections.RemoveRange(faceDetections);
            await dbContext.SaveChangesAsync();
        }

        // Reset flags on all shots
        foreach (var shot in shots)
        {
            shot.IsFaceProcessed = false;
            shot.NoFaces = false;
        }
        await dbContext.SaveChangesAsync();

        int totalShots = shots.Count;
        int facesDetected = 0;
        int shotsWithFaces = 0;
        int shotsWithoutFaces = 0;

        foreach (var shot in shots)
        {
            try
            {
                int detected = await faceDetectionService.DetectAndStoreFacesAsync(shot.ShotId);
                facesDetected += detected;
                if (detected > 0)
                    shotsWithFaces++;
                else
                    shotsWithoutFaces++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error detecting faces in shot {shot.ShotId}");
            }
        }

        // Run clustering for this user
        int newPersons = await faceClusteringService.ClusterUnassignedFacesAsync(userId.Value);

        return RedirectToAction("DetectPersonsResult", new
        {
            albumId = id,
            albumName = album.Name,
            totalShots,
            facesDetected,
            shotsWithFaces,
            shotsWithoutFaces,
            newPersons
        });
    }

    [Authorize]
    [HttpGet("detect_persons_result")]
    public IActionResult DetectPersonsResult(int albumId, string albumName, int totalShots, int facesDetected, int shotsWithFaces, int shotsWithoutFaces, int newPersons)
    {
        ViewBag.AlbumId = albumId;
        ViewBag.AlbumName = albumName;
        ViewBag.TotalShots = totalShots;
        ViewBag.FacesDetected = facesDetected;
        ViewBag.ShotsWithFaces = shotsWithFaces;
        ViewBag.ShotsWithoutFaces = shotsWithoutFaces;
        ViewBag.NewPersons = newPersons;
        return View();
    }

    [Authorize]
    [HttpPost("view_album")]
    public IActionResult ReloadAlbum(AlbumDTO dto, string? comment, string text, int id, int commentId)
    {
        if (comment != null)
        {
            commentService.AddComment(GetUsername(), text, id, commentId);
        }
        return RedirectToAction("ViewAlbum", new
        {
            Id = dto.AlbumId,
            dto.Token,
            dto.DateStart,
            dto.DateEnd,
            dto.LocationId,
            dto.North,
            dto.South,
            dto.West,
            dto.East
        });
    }

    [Authorize]
    [HttpGet("view_album")]
    public IActionResult ViewAlbum(
    int id, string? token, string? commentFilter,
    double? north, double? south, double? west, double? east)
    {
        AlbumDTO dto = new();
        dto.North = north ?? dto.North;
        dto.South = south ?? dto.South;
        dto.East = east ?? dto.East;
        dto.West = west ?? dto.West;
        dto.Token = token;
        dto.CommentFilter = commentFilter;

        var album = albumService.GetAuthorizedAlbum(id, GetUserId(), token);

        if (album == null)
        {
            return RedirectToAction("Albums");
        }

        dto.Shots = shotService.GetShotPreviews(id, dto.West, dto.East, dto.South, dto.North);

        // Filter shots by comment if comment filter is provided
        if (!string.IsNullOrEmpty(commentFilter))
        {
            var lowerFilter = commentFilter.ToLower();
            var shotIdsWithMatchingComments = dbContext.ShotComments
                .Where(c => c.Text.ToLower().Contains(lowerFilter))
                .Select(c => c.ShotId)
                .ToHashSet();
            dto.Shots = dto.Shots.Where(s => shotIdsWithMatchingComments.Contains(s.ShotId)).ToList();
        }

        dto.Placemarks = locationService.GetShotsForShots(dto.Shots);
        GeoRect rect = GeoRect.FromPlacemarks(dto.Placemarks, 0.1);
        dto.North = rect.North;
        dto.South = rect.South;
        dto.East = rect.East;
        dto.West = rect.West;
        dto.AlbumId = album.AlbumId;
        dto.Name = album.Name;
        dto.AlbumComments = album.AlbumComments ?? [];
        dto.Locations = locationService.GetLocations();

        var (prevId, nextId) = albumService.GetAdjacentAlbumIds(album.AlbumId);
        dto.PrevAlbumId = prevId;
        dto.NextAlbumId = nextId;

        return View(dto);

    }

    ///////////////////////////////////      SHOTS     ////////////////////////////////////

    [Authorize]
    [HttpGet("edit_shot")]
    public IActionResult EditShot(int id)
    {

        var currentUserId = GetUserId();

        var shot = shotService.GetShotWithAlbumAndUser(id, currentUserId!.Value);

        if (shot == null) return RedirectToAction("Albums");

        var album = albumService.GetAlbum(shot.AlbumId);
        var dto = new ShotDTO(shot!)
        {
            AlbumName = album?.Name ?? "",
            Locations = locationService.GetLocations(),
            Longitude = shot.Longitude,
            Latitude = shot.Latitude,
            Zoom = shot.Zoom,
            IsCover = album != null && shot.ShotId == album.PreviewId
        };
        return View(dto);
    }

    [Authorize]
    [HttpPost("edit_shot")]
    public IActionResult StoreShot(ShotDTO dto)
    {
        if (dto == null) return BadRequest();

        var shot = shotService.GetShot(dto.ShotId);
        if (shot == null) return NotFound();

        shotService.UpdateShot(dto);

        if (dto.IsCover)
        {
            shotService.UpdateShotAsAlbumPreview(dto.ShotId, shot.AlbumId);
        }

        locationService.AddLocationIfProvided(dto.LocationName, dto.Latitude, dto.Longitude, dto.Zoom);

        return Redirect("edit_album?id=" + shot.AlbumId);
    }

    [Authorize]
    [HttpGet("shots")]
    public IActionResult GetShots()
    {

        return View();
    }

    [Authorize]
    [HttpGet("preview")]
    public async Task<IActionResult> Preview(int id, int? rotate, bool? flip)
    {
        Console.WriteLine("PREVIEW `" + id);

        var preview = shotService.GetShotPreview(id);

        if (preview == null || preview.Length == 0)
            return NotFound();

        await using var stream = new MemoryStream(preview);

        string mimeType = "image/jpeg";
        if (rotate.HasValue || flip.HasValue)
        {
            var transformedStream = ImageUtils.GetTransformedImage(stream, rotate ?? 0, flip ?? false);
            return new FileStreamResult(transformedStream, mimeType);
        }

        return new FileStreamResult(stream, mimeType);
    }

    [Authorize]
    [HttpGet("shot")]
    public IActionResult Shot(int id, int? rotate, bool? flip)
    {
        var result = shotService.GetShot(id);
        if (result == null || result.FullScreen == null || result.FullScreen.Length == 0)
        {
            return NotFound();
        }

        using var stream = new MemoryStream();
        stream.Write(result.FullScreen, 0, result.FullScreen.Length);
        stream.Position = 0;

        string mimeType = "image/jpeg";
        if (rotate.HasValue || flip.HasValue)
        {
            var transformedStream = ImageUtils.GetTransformedImage(stream, rotate ?? 0, flip ?? false);
            return new FileStreamResult(transformedStream, mimeType);
        }

        return new FileStreamResult(stream, mimeType);
    }

    [Authorize]
    [HttpGet("orig")]
    public async Task<IActionResult> Orig(int id, string? token)
    {

        var currentUserId = GetUserId();

        var shot = shotService.GetAuthorizedShot(id, currentUserId, token);

        if (shot == null || shot.FullScreen == null)
        {
            return NotFound();
        }

        Stream? stream = await Storage.GetFile(shot);

        if (stream == null)
            return NotFound();

        return File(stream, shot.ContentType ?? "application/octet-stream");

    }

    [Authorize]
    [HttpGet("upload_shots")]
    public IActionResult UploadFile(int id)
    {
        UploadedFilesDTO dto = new() { AlbumId = id };
        return View(dto);
    }

    [Authorize]
    [RequestSizeLimit(1000_000_000)]
    [HttpPost("upload_shots")]
    public async Task<IActionResult> StoreFile(UploadedFilesDTO dto)
    {
        if (dto == null)
        {
            return BadRequest();
        }

        var files = dto.Files ?? [];
        long size = files.Sum(f => f.Length);
        dto.FileErrors = [];

        var user = userService.GetUserByUsername(GetUsername());
        if (user == null)
        {
            dto.ErrorMessage = "User not found";
            return View(dto);
        }

        var storage = userService.GetStorageForUser(user.UserId);
        if (storage == null)
        {
            dto.ErrorMessage = "No file storage available";
            return View(dto);
        }

        await dbContext.SaveChangesAsync();

        Album? album = null;
        if (dto.AlbumId != 0)
        {
            album = albumService.GetAlbumWithUser(dto.AlbumId, user.UserId);
            if (album == null)
            {
                dto.ErrorMessage = "Album not found";
                return View(dto);
            }
        }

        foreach (var formFile in files)
        {
            if (formFile == null) continue;
            if (formFile.Length > 0)
            {
                using var fileStream = formFile.OpenReadStream();
                byte[] bytes = new byte[formFile.Length];
                fileStream.Read(bytes, 0, (int)formFile.Length);
                var shot = new Shot();
                try
                {
                    PhotoMetadata metadata = fileService.GetMetadata(bytes, formFile.FileName, dto.FileErrors);

                    // Skip if metadata extraction failed
                    if (metadata == null)
                    {
                        Console.WriteLine($"[DEBUG] Skipping shot {formFile.FileName} because metadata is null.");
                        continue;
                    }

                    if (dto.AlbumId == 0)
                    {
                        if (metadata.CreationDate != null)
                        {
                            string albumName = metadata.CreationDate.Value.ToString("yyyy-MM-dd");
                            Album newAlbum = albumService.GetAlbumByName(albumName, user.UserId);
                            if (newAlbum == null)
                            {
                                newAlbum = new Album
                                {
                                    User = user,
                                    Name = albumName
                                };
                                albumService.CreateAlbum(newAlbum);
                            }
                            album = newAlbum;
                        }
                        else
                        {
                            dto.FileErrors.Add(formFile.FileName, "Can not create album when date is missing");
                            Console.WriteLine($"[DEBUG] Skipping shot {formFile.FileName} because date is missing and no album selected.");
                            continue;
                        }
                    }

                    if (album != null)
                    {
                        Console.WriteLine($"[DEBUG] Processing shot {formFile.FileName}. AlbumId: {album.AlbumId}, AlbumName: {album.Name}");
                        await ProcessShot(bytes, formFile.FileName, formFile.ContentType, shot, album, storage, dto.FileErrors, metadata);
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] Skipping shot {formFile.FileName} because album is null.");
                    }
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
                    dto.FileErrors.Add(formFile.FileName, e.Message);
                }
            }
        }
        return View(dto);
    }

    /////////////////////       LOCATIONS        //////////////////////////////////////////////////////////

    [Authorize]
    [HttpGet("delete_location")]
    public IActionResult DeleteLocation(int locationId)
    {
        locationService.DeleteLocation(locationId);
        return Redirect("locations");
    }

    [Authorize]
    [HttpGet("add_location")]
    public IActionResult AddLocation()
    {
        Location location = new();
        locationService.CreateLocation(location);
        return Redirect("edit_location?LocationId=" + location.Id);
    }

    [Authorize]
    [HttpGet("edit_location")]
    public IActionResult EditLocation(int locationId)
    {
        var location = locationService.GetLocation(locationId);
        if (location == null) return RedirectToAction("Locations");
        return View(location);
    }

    [Authorize]
    [HttpPost("edit_location")]
    public IActionResult SaveLocation(Location location)
    {
        if (location == null) return BadRequest();
        locationService.UpdateLocation(location);
        return Redirect("locations");
    }

    [Authorize]
    [HttpGet("view_shot")]
    public IActionResult ViewShot(int id, string? token)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} START GETTING SHOT " + id);

        var currentUserId = GetUserId();

        var shot = shotService.GetAuthorizedShot(id, currentUserId, token);

        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} END GETTING SHOT " + id);
        if (shot == null) return NotFound();
        var dto = new ShotDTO(shot)
        {
            Locations = locationService.GetLocations(),
            Longitude = shot.Longitude,
            Latitude = shot.Latitude,
            Zoom = shot.Zoom,
            Token = token,
            AlbumName = shot.Album.Name,
            ShotComments = commentService.GetShotComments(id)
        };
        return View(dto);
    }

    [Authorize]
    [HttpGet("delete_shot")]
    public async Task<IActionResult> DeleteShot(int id)
    {
        var shot = shotService.GetShot(id);
        if (shot == null) return NotFound();

        var albumId = shot.AlbumId;
        await shotService.DeleteShot(id);
        return Redirect("/edit_album?id=" + albumId);
    }

    [Authorize]
    [HttpGet("view_next_shot")]
    public IActionResult ViewNextShot(int id, string? token)
    {
        var shot = shotService.GetShot(id);
        if (shot == null) return Redirect("/view_shot?id=" + id);

        var nextShot = shotService.GetNextShot(shot.AlbumId, shot.ShotId);

        if (nextShot != null)
        {
            return Redirect($"/view_shot?id={nextShot.ShotId}&token={token}");
        }

        return Redirect($"/view_shot?id={id}&token={token}");
    }

    [Authorize]
    [HttpGet("view_prev_shot")]
    public IActionResult ViewPrevShot(int id, string? token)
    {
        var shot = shotService.GetShot(id);
        if (shot == null) return Redirect("/view_shot?id=" + id);

        var prevShot = shotService.GetPreviousShot(shot.AlbumId, shot.ShotId);

        if (prevShot != null)
        {
            return Redirect($"/view_shot?id={prevShot.ShotId}&token={token}");
        }

        return Redirect($"/view_shot?id={id}&token={token}");
    }

    [Authorize]
    [HttpGet("delete_comment")]
    public IActionResult DeleteComment(int commentId, int id)
    {
        commentService.DeleteAlbumComment(commentId);
        return Redirect("view_album?id=" + id);
    }

    [Authorize]
    [HttpPost("add_shot_comment")]
    public IActionResult AddShotComment(string text, int id, int commentId)
    {
        var user = userService.GetUserByUsername(GetUsername());
        if (user == null) return Unauthorized();

        commentService.AddShotComment(GetUsername(), text, id, commentId);
        return Redirect("view_shot?id=" + id);
    }

    [Authorize]
    [HttpGet("delete_shot_comment")]
    public IActionResult DeleteShotComment(int commentId, int id)
    {
        commentService.DeleteShotComment(commentId);
        return Redirect("view_shot?id=" + id);
    }

    [Authorize]
    [HttpGet("locations")]
    public IActionResult Locations()
    {
        var locations = locationService.GetLocations();
        return View(locations);
    }

    [Authorize]
    [HttpGet("profile")]
    public IActionResult Profile()
    {
        ProfileDTO dto = new();
        var user = userService.GetUserByUsername(GetUsername());
        dto.User = user;
        dto.Storages = user != null
            ? userService.GetStoragesForUser(user.UserId)
            : [];
        dto.SharedUsers = user != null
            ? userService.GetSharedUsers(user.UserId)
            : [];
        dto.SharedByUsers = user != null
            ? userService.GetHostsWhoSharedWithMe(user.UserId)
            : [];
        return View(dto);
    }

    [HttpGet("downloads")]
    public IActionResult Downloads()
    {
        return View();
    }

    [Authorize]
    [HttpGet("api/users/search")]
    public IActionResult SearchUsers([FromQuery] string q)
    {
        var user = userService.GetUserByUsername(GetUsername());
        if (user == null) return Unauthorized();

        var users = userService.SearchUsers(q, user.UserId);
        return Ok(users.Select(u => new { u.UserId, u.Username, u.Email }));
    }

    [Authorize]
    [HttpPost("api/shared-users")]
    public IActionResult AddSharedUser([FromBody] AddSharedUserDTO dto)
    {
        var user = userService.GetUserByUsername(GetUsername());
        if (user == null) return Unauthorized();

        var sharedUser = userService.AddSharedUser(user.UserId, dto.GuestUserId);
        return Ok(new { sharedUser.Id, sharedUser.GuestUserId, GuestUsername = sharedUser.GuestUser?.Username });
    }

    [Authorize]
    [HttpDelete("api/shared-users/{id}")]
    public IActionResult RemoveSharedUser(int id)
    {
        var user = userService.GetUserByUsername(GetUsername());
        if (user == null) return Unauthorized();

        var result = userService.RemoveSharedUser(id, user.UserId);
        if (!result) return NotFound();
        return Ok();
    }

    [Authorize]
    [HttpPost("api/shared-users/{id}/toggle-disabled")]
    public IActionResult ToggleSharedLibraryDisabled(int id)
    {
        var user = userService.GetUserByUsername(GetUsername());
        if (user == null) return Unauthorized();

        var disabled = userService.ToggleSharedUserDisabled(id, user.UserId);
        if (disabled == null) return NotFound();
        return Ok(new { disabled });
    }

    public class AddSharedUserDTO
    {
        public int GuestUserId { get; set; }
    }

    [Authorize]
    [HttpGet("edit_local_storage")]
    public IActionResult EditLocalStorage(int userId, int storageId)
    {
        StorageDTO dto = new();
        if (storageId != 0)
        {
            dto.Storage = userService.GetStorageById(storageId) ?? new ShotStorage();
        }
        else
        {
            string storageRoot = Environment.GetEnvironmentVariable("STORAGE_DIR") ?? "/storage";
            dto.Storage = new ShotStorage
            {
                Root = storageRoot,
                Provider = Provider.Local,
                UserId = userId
            };
        }
        return View(dto);
    }

    [Authorize]
    [HttpPost("edit_local_storage")]
    public IActionResult SaveLocalStorage(StorageDTO dto)
    {
        if (dto == null || dto.Storage == null) return BadRequest();
        userService.AddOrUpdateStorage(dto.Storage);
        return Redirect("profile?user_id=" + dto.Storage.UserId);
    }

    [Authorize]
    [Authorize]
    [HttpPost("select_album")]
    public IActionResult SelectAlbum(AlbumDTO dto)
    {
        if (dto == null) return RedirectToAction("Albums");

        var username = GetUsername() ?? string.Empty;
        var shots = (dto.Shots ?? []).Where(s => s != null && s.IsChecked).ToList();
        var albums = albumService.GetAlbumCardsForUser(username, dto.AlbumId);

        SelectAlbumDTO selectAlbumDTO = new()
        {
            Shots = shots,
            Albums = albums,
            SourceAlbumId = dto.AlbumId
        };

        return View(selectAlbumDTO);
    }

    [Authorize]
    [HttpPost("move_shots")]
    public IActionResult MoveShots(SelectAlbumDTO dto)
    {
        if (dto == null) return RedirectToAction("Albums");

        var shotsList = (dto.Shots ?? [])
            .Select(s => s.ShotId)
            .ToList();

        shotService.MoveShots(shotsList, dto.SourceAlbumId, dto.TargetAlbumId);
        albumService.UpdateAlbumPreview(dto.SourceAlbumId, shotsList, dto.TargetAlbumId);

        return Redirect("edit_album?id=" + dto.SourceAlbumId);
    }

    ///////////////////   FACE RECOGNITION  /////////////////////////////////////////

    [Authorize]
    [HttpPost("api/faces/detect/{shotId}")]
    public async Task<IActionResult> DetectFaces(int shotId)
    {
        var count = await faceDetectionService.DetectAndStoreFacesAsync(shotId);
        return Ok(new { count });
    }

    [Authorize(AuthenticationSchemes = "CookieScheme,Bearer")]
    [HttpPost("api/faces/cluster")]
    public async Task<IActionResult> ClusterFaces()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var count = await faceClusteringService.ClusterUnassignedFacesAsync(userId.Value);
        return Ok(new { count });
    }

    [Authorize]
    [HttpGet("api/faces/unconfirmed")]
    public async Task<IActionResult> GetUnconfirmedFaces()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var faces = await faceClusteringService.GetUnconfirmedFacesAsync(userId.Value);
        return Ok(faces);
    }

    [Authorize]
    [HttpGet("api/faces/unassigned")]
    public async Task<IActionResult> GetUnassignedFaces()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var faces = await faceClusteringService.GetUnassignedFacesAsync(userId.Value);
        return Ok(faces);
    }

    [Authorize]
    [HttpPost("api/faces/confirm/{faceId}")]
    public async Task<IActionResult> ConfirmFace(int faceId)
    {
        await personService.ConfirmFaceAssignmentAsync(faceId);
        return Ok();
    }

    [Authorize]
    [HttpPost("api/faces/confirm-batch")]
    public async Task<IActionResult> ConfirmFacesBatch([FromBody] List<int> faceIds)
    {
        var confirmed = await personService.ConfirmFacesAsync(faceIds);
        return Ok(new { success = true, message = $"Confirmed {confirmed} face(s)" });
    }

    [Authorize]
    [HttpPost("api/faces/reassign/{faceId}")]
    public async Task<IActionResult> ReassignFace(int faceId, [FromBody] int newPersonId)
    {
        await personService.ReassignFaceAsync(faceId, newPersonId);
        return Ok();
    }

    [Authorize]
    [HttpPost("api/faces/delete/{faceId}")]
    public async Task<IActionResult> DeleteFace(int faceId)
    {
        await personService.DeleteFaceAsync(faceId);
        return Ok();
    }

    [Authorize]
    [HttpPost("api/faces/delete-batch")]
    public async Task<IActionResult> DeleteFacesBatch([FromBody] List<int> faceIds)
    {
        var deleted = 0;
        foreach (var faceId in faceIds)
        {
            await personService.DeleteFaceAsync(faceId);
            deleted++;
        }
        return Ok(new { success = true, message = $"Deleted {deleted} face(s)" });
    }

    [Authorize]
    [HttpPost("api/faces/reassign-batch")]
    public async Task<IActionResult> ReassignFacesBatch([FromBody] BatchReassignRequest request)
    {
        var reassigned = 0;
        foreach (var faceId in request.FaceIds)
        {
            await personService.ReassignFaceAsync(faceId, request.PersonId);
            reassigned++;
        }
        return Ok(new { success = true, message = $"Reassigned {reassigned} face(s)" });
    }

    public class BatchReassignRequest
    {
        public List<int> FaceIds { get; set; }
        public int PersonId { get; set; }
    }

    [Authorize]
    [HttpPost("api/persons")]
    public async Task<IActionResult> CreatePerson([FromBody] PersonDTO dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var person = await personService.CreatePersonAsync(dto.FirstName, dto.LastName, userId.Value);
        return Ok(person);
    }

    [Authorize]
    [HttpGet("api/persons")]
    public async Task<IActionResult> GetPersons()
    {
        var persons = await personService.GetAllPersonsAsync();
        return Ok(persons);
    }

    [Authorize]
    [HttpGet("api/persons/{personId}/shots")]
    public async Task<IActionResult> GetPersonShots(int personId)
    {
        var shots = await personService.GetShotsForPersonAsync(personId);
        return Ok(shots);
    }

    [Authorize]
    [HttpGet("api/persons/{personId}/quality-metrics")]
    public async Task<IActionResult> GetPersonQualityMetrics(int personId)
    {
        var metrics = await personService.GetPersonQualityMetricsAsync(personId);
        if (metrics == null) return NotFound();
        return Ok(metrics);
    }

    [Authorize]
    [HttpGet("face/thumbnail/{id}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)] // Cache for 24 hours
    public async Task<IActionResult> GetFaceThumbnail(int id)
    {
        var bytes = await faceDetectionService.GetFaceImageAsync(id);
        if (bytes == null) return NotFound();

        // Add cache headers
        Response.Headers["Cache-Control"] = "public, max-age=86400";
        Response.Headers["ETag"] = $"\"{id}\"";

        return File(bytes, "image/jpeg");
    }

    [Authorize]
    [HttpGet("api/shots/{shotId}/faces")]
    public async Task<IActionResult> GetShotFaceDetections(int shotId)
    {
        var shot = await dbContext.Shots.FindAsync(shotId);
        if (shot?.FullScreen == null) return NotFound();

        // Load image to detect EXIF orientation (browser auto-applies EXIF, face detection did not)
        using var image = SixLabors.ImageSharp.Image.Load(shot.FullScreen);
        int rawWidth = image.Width;
        int rawHeight = image.Height;

        // Read EXIF orientation before AutoOrient clears it
        ushort exifOrientation = 1;
        var exifProfile = image.Metadata.ExifProfile;
        if (exifProfile != null && exifProfile.TryGetValue(ExifTag.Orientation, out var orientationValue))
            exifOrientation = orientationValue.Value;

        // Apply AutoOrient to get the browser-visible dimensions
        image.Mutate(x => x.AutoOrient());
        int imageWidth = image.Width;
        int imageHeight = image.Height;

        var faceDetections = await dbContext.FaceDetections
            .Include(fd => fd.Person)
            .Where(fd => fd.ShotId == shotId)
            .Select(fd => new
            {
                fd.X,
                fd.Y,
                fd.Width,
                fd.Height,
                personId = fd.PersonId,
                personName = fd.Person != null ? (fd.Person.FirstName + " " + fd.Person.LastName).Trim() : null
            })
            .ToListAsync();

        // Compute final image dimensions after EXIF orient + user Rotate
        int finalImageWidth = imageWidth;
        int finalImageHeight = imageHeight;
        if (shot.Rotate == 90 || shot.Rotate == 270)
        {
            finalImageWidth = imageHeight;
            finalImageHeight = imageWidth;
        }

        // Transform face coords: EXIF orientation first, then user Rotate/Flip (same order as image serving pipeline)
        var result = faceDetections.Select(fd =>
        {
            var (tx, ty, tw, th) = ApplyExifTransform(fd.X, fd.Y, fd.Width, fd.Height, exifOrientation, rawWidth, rawHeight);
            var (ux, uy, uw, uh) = ApplyUserRotateFlip(tx, ty, tw, th, shot.Rotate, shot.Flip, imageWidth, imageHeight);
            return new
            {
                x = ux,
                y = uy,
                width = uw,
                height = uh,
                imageWidth = finalImageWidth,
                imageHeight = finalImageHeight,
                fd.personId,
                fd.personName
            };
        }).ToList();

        return Ok(result);
    }

    private static (int x, int y, int w, int h) ApplyExifTransform(int x, int y, int w, int h, ushort orientation, int rawW, int rawH)
    {
        return orientation switch
        {
            1 => (x, y, w, h),                                  // Normal — no change
            2 => (rawW - x - w, y, w, h),                       // Flip horizontal
            3 => (rawW - x - w, rawH - y - h, w, h),            // Rotate 180°
            4 => (x, rawH - y - h, w, h),                       // Flip vertical
            5 => (y, x, h, w),                                   // Transpose (90° CCW + flip horizontal)
            6 => (rawH - y - h, x, h, w),                       // Rotate 90° CW
            7 => (rawH - y - h, rawW - x - w, h, w),            // Transverse (90° CW + flip horizontal)
            8 => (y, rawW - x - w, h, w),                       // Rotate 90° CCW
            _ => (x, y, w, h)
        };
    }

    private static (int x, int y, int w, int h) ApplyUserRotateFlip(int x, int y, int w, int h, int rotate, bool flip, int imgW, int imgH)
    {
        int nx = x, ny = y, nw = w, nh = h;
        int curW = imgW, curH = imgH;

        switch (rotate)
        {
            case 90:
                nx = curH - y - h; ny = x; nw = h; nh = w;
                curW = imgH; curH = imgW;
                break;
            case 180:
                nx = curW - x - w; ny = curH - y - h;
                break;
            case 270:
                nx = y; ny = curW - x - w; nw = h; nh = w;
                curW = imgH; curH = imgW;
                break;
        }

        if (flip)
            nx = curW - nx - nw;

        return (nx, ny, nw, nh);
    }

    [Authorize]
    [HttpGet("person/preview/{id}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetPersonPreview(int id)
    {
        var bytes = await personService.GetPersonPreviewAsync(id);
        if (bytes == null) return NotFound();

        Response.Headers["Cache-Control"] = "public, max-age=86400";
        Response.Headers["ETag"] = $"\"p{id}\"";

        return File(bytes, "image/jpeg");
    }

    [Authorize]
    [HttpGet("api/album/{albumId}/face-similarities")]
    public async Task<IActionResult> GetAlbumFaceSimilarities(int albumId)
    {
        var faces = await dbContext.FaceDetections
            .Include(fd => fd.FaceEncoding)
            .Where(fd => fd.Shot.AlbumId == albumId && fd.FaceEncoding != null)
            .Select(fd => new { fd.FaceDetectionId, fd.ShotId, fd.Width, fd.Height, fd.FaceEncoding.Encoding })
            .ToListAsync();

        var results = new List<object>();

        for (int i = 0; i < faces.Count; i++)
        {
            for (int j = i + 1; j < faces.Count; j++)
            {
                var enc1 = BytesToFloats(faces[i].Encoding);
                var enc2 = BytesToFloats(faces[j].Encoding);
                var similarity = CosineSimilarity(enc1, enc2);

                results.Add(new {
                    face1 = faces[i].FaceDetectionId,
                    shot1 = faces[i].ShotId,
                    size1 = $"{faces[i].Width}x{faces[i].Height}",
                    face2 = faces[j].FaceDetectionId,
                    shot2 = faces[j].ShotId,
                    size2 = $"{faces[j].Width}x{faces[j].Height}",
                    similarity = Math.Round(similarity, 4)
                });
            }
        }

        return Ok(results.OrderByDescending(r => ((dynamic)r).similarity));
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    [Authorize]
    [HttpGet("persons/bloated")]
    public async Task<IActionResult> BloatedPersons()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bloatedPersons = await faceClusteringService.FindBloatedPersonsAsync(userId.Value);

        ViewBag.TotalBloated = bloatedPersons.Count;
        return View(bloatedPersons);
    }

    [Authorize]
    [HttpGet("persons")]
    public async Task<IActionResult> PersonsList(int page = 1, int pageSize = 24, string search = null)
    {
        // Optimized query: only load what we need for display, excluding faces from shots marked as "no faces"
        var baseQuery = dbContext.Persons.AsNoTracking();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            baseQuery = baseQuery.Where(p =>
                p.FirstName.ToLower().Contains(searchLower) ||
                p.LastName.ToLower().Contains(searchLower) ||
                (p.FirstName + " " + p.LastName).ToLower().Contains(searchLower));
        }

        var personsQuery = baseQuery
            .Select(p => new
            {
                Person = p,
                FirstFaceId = p.FaceDetections
                    .Where(fd => fd.Shot != null && !fd.Shot.NoFaces)
                    .OrderBy(fd => fd.DetectedAt)
                    .Select(fd => fd.FaceDetectionId)
                    .FirstOrDefault(),
                FaceCount = p.FaceDetections.Count(fd => fd.Shot != null && !fd.Shot.NoFaces)
            })
            .OrderByDescending(x => x.FaceCount)
            .ThenBy(x => x.Person.FirstName)
            .ThenBy(x => x.Person.LastName);

        var totalCount = await personsQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Ensure page is within valid range
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var personData = await personsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Create a lightweight model for the view
        var persons = personData.Select(pd =>
        {
            pd.Person.FaceDetections = new List<FaceDetection>();
            if (pd.FirstFaceId > 0)
            {
                pd.Person.FaceDetections.Add(new FaceDetection { FaceDetectionId = pd.FirstFaceId });
            }
            return pd.Person;
        }).ToList();

        // Store face counts separately
        ViewBag.FaceCounts = personData.ToDictionary(pd => pd.Person.PersonId, pd => pd.FaceCount);
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.PageSize = pageSize;
        ViewBag.Search = search;

        return View(persons);
    }

    [Authorize]
    [HttpGet("person/{id}")]
    public async Task<IActionResult> Person(int id, int page = 1, int pageSize = 48)
    {
        var person = await dbContext.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == id);
        if (person == null) return NotFound();

        // Count total faces (lightweight query without loading Shot data)
        var totalCount = await dbContext.FaceDetections
            .AsNoTracking()
            .Where(fd => fd.PersonId == id && fd.Shot != null && !fd.Shot.NoFaces)
            .CountAsync();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Ensure page is within valid range
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        // Optimized query: only select needed fields, don't load FullScreen images
        var validFaceDetections = await dbContext.FaceDetections
            .AsNoTracking()
            .Where(fd => fd.PersonId == id && fd.Shot != null && !fd.Shot.NoFaces)
            .OrderBy(fd => fd.Shot.DateStart)
            .ThenBy(fd => fd.ShotId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(fd => new FaceDetection
            {
                FaceDetectionId = fd.FaceDetectionId,
                ShotId = fd.ShotId,
                PersonId = fd.PersonId,
                IsConfirmed = fd.IsConfirmed,
                Quality = fd.Quality,
                DetectedAt = fd.DetectedAt,
                Shot = new Shot
                {
                    ShotId = fd.Shot.ShotId,
                    DateStart = fd.Shot.DateStart,
                    Flip = fd.Shot.Flip,
                    Rotate = fd.Shot.Rotate
                    // Don't load Preview, FullScreen, or other large fields
                }
            })
            .ToListAsync();

        // Only load named persons for dropdown (much smaller set)
        var namedPersons = await dbContext.Persons
            .AsNoTracking()
            .Where(p => p.PersonId != id
                && p.FirstName != null
                && p.FirstName != ""
                && p.FirstName != "Person")
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .Select(p => new { p.PersonId, p.FirstName, p.LastName })
            .ToListAsync();

        ViewBag.Person = person;
        ViewBag.AllPersons = namedPersons.Select(p => new Person
        {
            PersonId = p.PersonId,
            FirstName = p.FirstName,
            LastName = p.LastName
        }).ToList();
        ViewBag.FaceCount = totalCount;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;
        ViewBag.QualityMetrics = null; // Load async via API if needed

        return View(validFaceDetections);
    }

    [Authorize]
    [HttpPost("merge_persons")]
    public async Task<IActionResult> MergePersons([FromForm] List<int> selectedPersons)
    {
        try
        {
            if (selectedPersons == null || selectedPersons.Count < 2)
            {
                return BadRequest("Please select at least 2 persons to merge.");
            }

            // Load all selected persons
            var persons = await dbContext.Persons
                .Where(p => selectedPersons.Contains(p.PersonId))
                .ToListAsync();

            // Find the person with a real name (not "Person #...")
            var personWithRealName = persons.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.FirstName) &&
                !p.FirstName.Equals("Person", StringComparison.OrdinalIgnoreCase) &&
                !p.LastName.StartsWith("#"));

            // If no one has a real name, use the first one
            var targetPersonId = personWithRealName?.PersonId ?? selectedPersons[0];

            // Merge all others into the target
            foreach (var personId in selectedPersons.Where(id => id != targetPersonId))
            {
                await personService.MergePeopleAsync(personId, targetPersonId);
            }

            return Redirect("/persons");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error merging persons");
            return BadRequest($"Error merging persons: {ex.Message}");
        }
    }

    [Authorize]
    [HttpPost("update_person_name/{id}")]
    public async Task<IActionResult> UpdatePersonName(int id, [FromBody] UpdatePersonNameDTO dto)
    {
        var person = await dbContext.Persons.FindAsync(id);
        if (person == null) return NotFound();

        person.FirstName = string.IsNullOrWhiteSpace(dto.FirstName) ? "Person" : dto.FirstName;
        person.LastName = string.IsNullOrWhiteSpace(dto.LastName) ? $"#{id}" : dto.LastName;

        await dbContext.SaveChangesAsync();
        return Ok();
    }

    public class UpdatePersonNameDTO
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    [Authorize]
    [HttpGet("delete_person/{id}")]
    public async Task<IActionResult> DeletePerson(int id)
    {
        var person = await dbContext.Persons
            .Include(p => p.FaceDetections)
            .FirstOrDefaultAsync(p => p.PersonId == id);

        if (person == null) return NotFound();

        // Unassign all face detections
        foreach (var face in person.FaceDetections)
        {
            face.PersonId = null;
        }

        dbContext.Persons.Remove(person);
        await dbContext.SaveChangesAsync();

        return Redirect("/persons");
    }

    [Authorize]
    [HttpGet("faces/review")]
    public async Task<IActionResult> ReviewFaces(int unconfirmedPage = 1, int unassignedPage = 1, int pageSize = 24)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (unconfirmedFaces, unconfirmedTotal) = await faceClusteringService.GetUnconfirmedFacesAsync(userId.Value, unconfirmedPage, pageSize);
        var (unassignedFaces, unassignedTotal) = await faceClusteringService.GetUnassignedFacesAsync(userId.Value, unassignedPage, pageSize);

        var allPersons = await personService.GetAllPersonsAsync();
        var filteredPersons = allPersons.Where(p => !p.LastName.StartsWith("#")).ToList();

        var model = new FaceReviewViewModel
        {
            Unconfirmed = unconfirmedFaces,
            UnconfirmedTotalCount = unconfirmedTotal,
            UnconfirmedCurrentPage = unconfirmedPage,
            UnconfirmedTotalPages = (int)Math.Ceiling(unconfirmedTotal / (double)pageSize),
            Unassigned = unassignedFaces,
            UnassignedTotalCount = unassignedTotal,
            UnassignedCurrentPage = unassignedPage,
            UnassignedTotalPages = (int)Math.Ceiling(unassignedTotal / (double)pageSize),
            PageSize = pageSize,
            Persons = filteredPersons
        };

        return View(model);
    }

    public class FaceReviewViewModel
    {
        public List<FaceDetection> Unconfirmed { get; set; }
        public int UnconfirmedTotalCount { get; set; }
        public int UnconfirmedCurrentPage { get; set; }
        public int UnconfirmedTotalPages { get; set; }
        public List<FaceDetection> Unassigned { get; set; }
        public int UnassignedTotalCount { get; set; }
        public int UnassignedCurrentPage { get; set; }
        public int UnassignedTotalPages { get; set; }
        public int PageSize { get; set; }
        public List<Person> Persons { get; set; }
    }

    [Authorize]
    [HttpGet("faces/unconfirmed")]
    public async Task<IActionResult> UnconfirmedFaces(int page = 1, int pageSize = 48)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (faces, total) = await faceClusteringService.GetUnconfirmedFacesAsync(userId.Value, page, pageSize);
        var allPersons = await personService.GetAllPersonsAsync();
        var filteredPersons = allPersons.Where(p => !p.LastName.StartsWith("#")).ToList();

        var model = new FaceListViewModel
        {
            Faces = faces,
            TotalCount = total,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            PageSize = pageSize,
            Persons = filteredPersons
        };

        return View(model);
    }

    [Authorize]
    [HttpGet("faces/unassigned")]
    public async Task<IActionResult> UnassignedFaces(int page = 1, int pageSize = 48)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (faces, total) = await faceClusteringService.GetUnassignedFacesAsync(userId.Value, page, pageSize);
        var allPersons = await personService.GetAllPersonsAsync();
        var filteredPersons = allPersons.Where(p => !p.LastName.StartsWith("#")).ToList();

        var model = new FaceListViewModel
        {
            Faces = faces,
            TotalCount = total,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            PageSize = pageSize,
            Persons = filteredPersons
        };

        return View(model);
    }

    public class FaceListViewModel
    {
        public List<FaceDetection> Faces { get; set; }
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public List<Person> Persons { get; set; }
    }

    public class PersonDTO
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }


    public IEnumerable<Album> SortAlbums(IEnumerable<Album> albums, SortBy sortBy, SortDirection direction)
    {
        if (albums == null) return [];

        Func<Album, object> keySelector = sortBy switch
        {
            SortBy.EarliestDate => a => a.Shots != null && a.Shots.Count > 0 ? a.Shots.Min(s => s.DateStart) : DateTime.MinValue,
            SortBy.LeastLatitude => a => a.Shots != null && a.Shots.Count > 0 ? a.Shots.Min(s => s.Latitude) : 0.0,
            SortBy.LeastLongitude => a => a.Shots != null && a.Shots.Count > 0 ? a.Shots.Min(s => s.Longitude) : 0.0,
            SortBy.ShotCount => a => a.Shots != null ? a.Shots.Count : 0,
            _ => a => a.Name ?? string.Empty
        };

        return direction == SortDirection.Ascending
            ? albums.OrderBy(keySelector)
            : albums.OrderByDescending(keySelector);
    }

    [Authorize]
    [HttpGet("same_day")]
    public IActionResult SameDay(int month, int day)
    {
        var shots = shotService.GetSameDayShots(month, day, 0);
        return View(shots);
    }

    [Authorize]
    [HttpGet("settings")]
    public async Task<IActionResult> Settings()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var settingsService = new ClusteringSettingsService(dbContext);
        var settings = await settingsService.GetOrCreateSettingsAsync(userId.Value);

        return View(settings);
    }

    [Authorize]
    [HttpPost("settings/update")]
    public async Task<IActionResult> UpdateSettings([FromForm] string preset, [FromForm] float? threshold, [FromForm] int? minFaces, [FromForm] int? minSize, [FromForm] float? minQuality, [FromForm] float? autoMerge)
    {
        var userId = GetUserId();
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
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var settingsService = new ClusteringSettingsService(dbContext);
        var isSuspended = await settingsService.ToggleProcessingSuspendedAsync(userId.Value);

        return Json(new { suspended = isSuspended });
    }

    [Authorize]
    [HttpPost("settings/delete-all-faces")]
    public async Task<IActionResult> DeleteAllFaces()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        // Delete all face data for this user
        var userAlbums = await dbContext.Albums
            .Where(a => a.User.UserId == userId.Value)
            .Select(a => a.AlbumId)
            .ToListAsync();

        var shotIds = await dbContext.Shots
            .Where(s => userAlbums.Contains(s.AlbumId))
            .Select(s => s.ShotId)
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

        // Delete persons for this user (find persons via their face detections)
        var personIds = detections.Where(fd => fd.PersonId.HasValue).Select(fd => fd.PersonId.Value).Distinct().ToList();
        var persons = await dbContext.Persons
            .Where(p => personIds.Contains(p.PersonId))
            .ToListAsync();
        dbContext.Persons.RemoveRange(persons);

        // Reset face processing flag
        var shots = await dbContext.Shots
            .Where(s => shotIds.Contains(s.ShotId))
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

    [Authorize]
    [HttpPost("person/{personId}/reassign-face/{faceId}")]
    public async Task<IActionResult> ReassignFace(int personId, int faceId, [FromBody] ReassignFaceDTO dto)
    {
        var face = await dbContext.FaceDetections.FindAsync(faceId);
        if (face == null || face.PersonId != personId) return NotFound();

        // Reassign to new person or make unassigned
        face.PersonId = dto.NewPersonId == 0 ? null : dto.NewPersonId;
        face.IsConfirmed = dto.NewPersonId.HasValue;

        await dbContext.SaveChangesAsync();

        return Json(new { success = true, message = "Face reassigned successfully." });
    }

    public class ReassignFaceDTO
    {
        public int? NewPersonId { get; set; }
    }

    [Authorize]
    [HttpPost("person/{personId}/reassign-faces-batch")]
    public async Task<IActionResult> ReassignFacesBatch(int personId, [FromBody] ReassignFacesBatchDTO dto)
    {
        var faces = await dbContext.FaceDetections
            .Where(fd => dto.FaceIds.Contains(fd.FaceDetectionId) && fd.PersonId == personId)
            .ToListAsync();

        if (!faces.Any()) return NotFound();

        foreach (var face in faces)
        {
            face.PersonId = dto.NewPersonId == 0 ? null : dto.NewPersonId;
            face.IsConfirmed = dto.NewPersonId.HasValue;
        }

        await dbContext.SaveChangesAsync();

        return Json(new { success = true, message = $"Reassigned {faces.Count} faces successfully." });
    }

    public class ReassignFacesBatchDTO
    {
        public List<int> FaceIds { get; set; } = new List<int>();
        public int? NewPersonId { get; set; }
    }

    [Authorize]
    [HttpPost("person/{personId}/set-profile-photo")]
    public async Task<IActionResult> SetPersonProfilePhoto(int personId, [FromBody] SetProfilePhotoDTO dto)
    {
        logger.LogInformation($"[SetProfilePhoto] Setting profile photo for person {personId} to shot {dto.ShotId}");

        var person = await dbContext.Persons.FindAsync(personId);
        if (person == null)
        {
            logger.LogWarning($"[SetProfilePhoto] Person {personId} not found");
            return NotFound();
        }

        var shot = await dbContext.Shots.FindAsync(dto.ShotId);
        if (shot == null)
        {
            logger.LogWarning($"[SetProfilePhoto] Shot {dto.ShotId} not found");
            return NotFound(new { message = "Shot not found" });
        }

        logger.LogInformation($"[SetProfilePhoto] Person before update: ProfilePhotoId = {person.ProfilePhotoId}");
        await personService.SetProfilePhotoAsync(personId, dto.ShotId);

        // Re-query to verify the change was saved
        var updatedPerson = await dbContext.Persons.FindAsync(personId);
        logger.LogInformation($"[SetProfilePhoto] Person after update: ProfilePhotoId = {updatedPerson?.ProfilePhotoId}");

        return Json(new { success = true, message = "Profile photo set successfully." });
    }

    public class SetProfilePhotoDTO
    {
        public int ShotId { get; set; }
    }


    [Authorize]
    [HttpPost("shot/{shotId}/exclude-from-face-detection")]
    public async Task<IActionResult> ExcludeFromFaceDetection(int shotId)
    {
        var shot = await dbContext.Shots.FindAsync(shotId);
        if (shot == null) return NotFound();

        // Remove all face detections for this shot
        var faceDetections = await dbContext.FaceDetections
            .Where(fd => fd.ShotId == shotId)
            .ToListAsync();

        if (faceDetections.Any())
        {
            // Get face detection IDs
            var faceDetectionIds = faceDetections.Select(fd => fd.FaceDetectionId).ToList();

            // Remove encodings first
            var encodings = await dbContext.FaceEncodings
                .Where(fe => faceDetectionIds.Contains(fe.FaceDetectionId))
                .ToListAsync();
            dbContext.FaceEncodings.RemoveRange(encodings);

            // Remove face detections
            dbContext.FaceDetections.RemoveRange(faceDetections);
        }

        // Mark as no faces and processed to exclude from future processing
        shot.NoFaces = true;
        shot.IsFaceProcessed = true;

        await dbContext.SaveChangesAsync();

        return Json(new {
            success = true,
            message = $"Shot excluded from face detection. Removed {faceDetections.Count} face detection(s)."
        });
    }

    [Authorize]
    [HttpPost("shot/{shotId}/include-in-face-detection")]
    public async Task<IActionResult> IncludeInFaceDetection(int shotId)
    {
        var shot = await dbContext.Shots.FindAsync(shotId);
        if (shot == null) return NotFound();

        // Mark as not processed and clear no_faces flag so it will be picked up by background service
        shot.NoFaces = false;
        shot.IsFaceProcessed = false;

        await dbContext.SaveChangesAsync();

        return Json(new {
            success = true,
            message = "Shot will be processed for face detection."
        });
    }

    [Authorize]
    [HttpPost("shots/exclude-batch-from-face-detection")]
    public async Task<IActionResult> ExcludeBatchFromFaceDetection([FromBody] ExcludeBatchDTO dto)
    {
        if (dto.ShotIds == null || !dto.ShotIds.Any())
        {
            return BadRequest(new { message = "No shot IDs provided" });
        }

        var shots = await dbContext.Shots
            .Where(s => dto.ShotIds.Contains(s.ShotId))
            .ToListAsync();

        if (!shots.Any()) return NotFound();

        // Remove all face detections for these shots
        var faceDetections = await dbContext.FaceDetections
            .Where(fd => dto.ShotIds.Contains(fd.ShotId))
            .ToListAsync();

        if (faceDetections.Any())
        {
            // Get face detection IDs
            var faceDetectionIds = faceDetections.Select(fd => fd.FaceDetectionId).ToList();

            // Remove encodings first
            var encodings = await dbContext.FaceEncodings
                .Where(fe => faceDetectionIds.Contains(fe.FaceDetectionId))
                .ToListAsync();
            dbContext.FaceEncodings.RemoveRange(encodings);

            // Remove face detections
            dbContext.FaceDetections.RemoveRange(faceDetections);
        }

        // Mark all as no faces and processed to exclude from future processing
        foreach (var shot in shots)
        {
            shot.NoFaces = true;
            shot.IsFaceProcessed = true;
        }

        await dbContext.SaveChangesAsync();

        return Json(new {
            success = true,
            message = $"Excluded {shots.Count} shot(s) from face detection. Removed {faceDetections.Count} face detection(s)."
        });
    }

    [Authorize]
    [HttpPost("shots/include-batch-in-face-detection")]
    public async Task<IActionResult> IncludeBatchInFaceDetection([FromBody] ExcludeBatchDTO dto)
    {
        if (dto.ShotIds == null || !dto.ShotIds.Any())
        {
            return BadRequest(new { message = "No shot IDs provided" });
        }

        var shots = await dbContext.Shots
            .Where(s => dto.ShotIds.Contains(s.ShotId))
            .ToListAsync();

        if (!shots.Any()) return NotFound();

        // Mark all as not processed and clear no_faces flag so they will be picked up by background service
        foreach (var shot in shots)
        {
            shot.NoFaces = false;
            shot.IsFaceProcessed = false;
        }

        await dbContext.SaveChangesAsync();

        return Json(new {
            success = true,
            message = $"{shots.Count} shot(s) will be processed for face detection."
        });
    }

    public class ExcludeBatchDTO
    {
        public List<int> ShotIds { get; set; } = new List<int>();
    }

    ///////////////////   ADMIN STATS  /////////////////////////////////////////

    [Authorize]
    [HttpGet("admin/stats")]
    public async Task<IActionResult> AdminStats()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        // Get bot user IDs for caption stats
        var botUserIds = await dbContext.Users
            .Where(u => u.Username.StartsWith("bot-"))
            .Select(u => u.UserId)
            .ToListAsync();

        // Get album basic stats
        var albumStats = await dbContext.Albums
            .Where(a => a.User.UserId == userId.Value)
            .Select(a => new AlbumStatsDTO
            {
                AlbumId = a.AlbumId,
                AlbumName = a.Name,
                TotalShots = a.Shots!.Count(),
                ProcessedShots = a.Shots!.Count(s => s.IsFaceProcessed),
                ShotsWithFaces = a.Shots!.Count(s => s.IsFaceProcessed && !s.NoFaces),
                ShotsWithoutFaces = a.Shots!.Count(s => s.NoFaces),
                TotalComments = a.AlbumComments!.Count() + a.Shots!.SelectMany(s => s.ShotComments).Count(),
                EarliestDate = a.Shots!.Min(s => (DateTime?)s.DateStart),
                LatestDate = a.Shots!.Max(s => (DateTime?)s.DateStart)
            })
            .OrderByDescending(a => a.EarliestDate)
            .ToListAsync();

        // Get face counts per album separately
        var faceCounts = await dbContext.FaceDetections
            .Where(fd => fd.Shot.Album.User.UserId == userId.Value)
            .GroupBy(fd => fd.Shot.AlbumId)
            .Select(g => new { AlbumId = g.Key, Count = g.Count() })
            .ToListAsync();

        var faceCountDict = faceCounts.ToDictionary(x => x.AlbumId, x => x.Count);

        // Get caption counts per album (shots with bot comments)
        var captionCounts = await dbContext.ShotComments
            .Where(c => botUserIds.Contains(c.AuthorId) && c.Shot.Album.User.UserId == userId.Value)
            .GroupBy(c => c.Shot.AlbumId)
            .Select(g => new { AlbumId = g.Key, Count = g.Count() })
            .ToListAsync();

        var captionCountDict = captionCounts.ToDictionary(x => x.AlbumId, x => x.Count);

        foreach (var album in albumStats)
        {
            album.TotalFaces = faceCountDict.GetValueOrDefault(album.AlbumId, 0);
            album.CaptionedShots = captionCountDict.GetValueOrDefault(album.AlbumId, 0);
        }

        // Get years stats - basic aggregations only
        var yearStatsRaw = await dbContext.Shots
            .Where(s => s.Album.User.UserId == userId.Value)
            .GroupBy(s => s.DateStart.Year)
            .Select(g => new YearStatsDTO
            {
                Year = g.Key,
                TotalShots = g.Count(),
                ProcessedShots = g.Count(s => s.IsFaceProcessed),
                ShotsWithFaces = g.Count(s => s.IsFaceProcessed && !s.NoFaces),
                ShotsWithoutFaces = g.Count(s => s.NoFaces),
                TotalComments = g.SelectMany(s => s.ShotComments).Count(),
                AlbumCount = g.Select(s => s.AlbumId).Distinct().Count()
            })
            .ToListAsync();

        // Get face counts per year separately
        var faceCountsByYear = await dbContext.FaceDetections
            .Where(fd => fd.Shot.Album.User.UserId == userId.Value)
            .GroupBy(fd => fd.Shot.DateStart.Year)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .ToListAsync();

        var yearFaceDict = faceCountsByYear.ToDictionary(x => x.Year, x => x.Count);

        // Get caption counts per year
        var captionCountsByYear = await dbContext.ShotComments
            .Where(c => botUserIds.Contains(c.AuthorId) && c.Shot.Album.User.UserId == userId.Value)
            .GroupBy(c => c.Shot.DateStart.Year)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .ToListAsync();

        var yearCaptionDict = captionCountsByYear.ToDictionary(x => x.Year, x => x.Count);

        // Fill in all years from min to max, including years with 0 shots
        var yearStatsDict = yearStatsRaw.ToDictionary(y => y.Year);
        int minYear = yearStatsRaw.Any() ? yearStatsRaw.Min(y => y.Year) : DateTime.Now.Year;
        int maxYear = yearStatsRaw.Any() ? yearStatsRaw.Max(y => y.Year) : DateTime.Now.Year;

        var yearStats = new List<YearStatsDTO>();
        for (int year = maxYear; year >= minYear; year--)
        {
            if (yearStatsDict.TryGetValue(year, out var existing))
            {
                existing.TotalFaces = yearFaceDict.GetValueOrDefault(year, 0);
                existing.CaptionedShots = yearCaptionDict.GetValueOrDefault(year, 0);
                yearStats.Add(existing);
            }
            else
            {
                // Year with no shots - add empty entry
                yearStats.Add(new YearStatsDTO
                {
                    Year = year,
                    TotalShots = 0,
                    ProcessedShots = 0,
                    ShotsWithFaces = 0,
                    ShotsWithoutFaces = 0,
                    TotalFaces = 0,
                    TotalComments = 0,
                    CaptionedShots = 0,
                    AlbumCount = 0
                });
            }
        }

        var model = new AdminStatsViewModel
        {
            AlbumStats = albumStats,
            YearStats = yearStats
        };

        return View(model);
    }

    [Authorize]
    [HttpGet("admin/processing-histogram")]
    public async Task<IActionResult> ProcessingHistogram(string period = "day", int days = 30)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var startDate = DateTime.UtcNow.AddDays(-days);

        // Get user's shot IDs
        var userShotIds = await dbContext.Shots
            .Where(s => s.Album.User.UserId == userId.Value)
            .Select(s => s.ShotId)
            .ToListAsync();

        // Face detection stats
        var faceDetections = await dbContext.FaceDetections
            .Where(fd => userShotIds.Contains(fd.ShotId) && fd.DetectedAt >= startDate)
            .Select(fd => fd.DetectedAt)
            .ToListAsync();

        // Caption stats (bot comments)
        var botUsernames = await dbContext.Users
            .Where(u => u.Username.StartsWith("bot-"))
            .Select(u => u.UserId)
            .ToListAsync();

        var captions = await dbContext.ShotComments
            .Where(c => botUsernames.Contains(c.AuthorId) &&
                       userShotIds.Contains(c.ShotId) &&
                       c.Timestamp >= startDate)
            .Select(c => c.Timestamp)
            .ToListAsync();

        // Group by period
        var faceHistogram = GroupByPeriod(faceDetections, period);
        var captionHistogram = GroupByPeriod(captions, period);

        // Merge all dates
        var allDates = faceHistogram.Keys.Union(captionHistogram.Keys).OrderBy(d => d).ToList();

        var model = new ProcessingHistogramViewModel
        {
            Period = period,
            Days = days,
            Labels = allDates.Select(d => FormatLabel(d, period)).ToList(),
            FaceDetectionCounts = allDates.Select(d => faceHistogram.GetValueOrDefault(d, 0)).ToList(),
            CaptionCounts = allDates.Select(d => captionHistogram.GetValueOrDefault(d, 0)).ToList()
        };

        return View(model);
    }

    [Authorize]
    [HttpGet("api/admin/processing-data")]
    public async Task<IActionResult> GetProcessingData(string period = "day", int days = 30)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        var startDate = DateTime.UtcNow.AddDays(-days);

        var userShotIds = await dbContext.Shots
            .Where(s => s.Album.User.UserId == userId.Value)
            .Select(s => s.ShotId)
            .ToListAsync();

        var faceDetections = await dbContext.FaceDetections
            .Where(fd => userShotIds.Contains(fd.ShotId) && fd.DetectedAt >= startDate)
            .Select(fd => fd.DetectedAt)
            .ToListAsync();

        var botUsernames = await dbContext.Users
            .Where(u => u.Username.StartsWith("bot-"))
            .Select(u => u.UserId)
            .ToListAsync();

        var captions = await dbContext.ShotComments
            .Where(c => botUsernames.Contains(c.AuthorId) &&
                       userShotIds.Contains(c.ShotId) &&
                       c.Timestamp >= startDate)
            .Select(c => c.Timestamp)
            .ToListAsync();

        var faceHistogram = GroupByPeriod(faceDetections, period);
        var captionHistogram = GroupByPeriod(captions, period);

        var allDates = faceHistogram.Keys.Union(captionHistogram.Keys).OrderBy(d => d).ToList();

        return Ok(new
        {
            labels = allDates.Select(d => FormatLabel(d, period)).ToList(),
            faceDetection = allDates.Select(d => faceHistogram.GetValueOrDefault(d, 0)).ToList(),
            captions = allDates.Select(d => captionHistogram.GetValueOrDefault(d, 0)).ToList()
        });
    }

    private Dictionary<DateTime, int> GroupByPeriod(List<DateTime> dates, string period)
    {
        return period switch
        {
            "hour" => dates.GroupBy(d => new DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0))
                          .ToDictionary(g => g.Key, g => g.Count()),
            "day" => dates.GroupBy(d => d.Date)
                         .ToDictionary(g => g.Key, g => g.Count()),
            "week" => dates.GroupBy(d => d.Date.AddDays(-(int)d.DayOfWeek))
                          .ToDictionary(g => g.Key, g => g.Count()),
            "month" => dates.GroupBy(d => new DateTime(d.Year, d.Month, 1))
                           .ToDictionary(g => g.Key, g => g.Count()),
            _ => dates.GroupBy(d => d.Date)
                     .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private string FormatLabel(DateTime date, string period)
    {
        return period switch
        {
            "hour" => date.ToString("MM/dd HH:00"),
            "day" => date.ToString("MM/dd"),
            "week" => $"Week {date:MM/dd}",
            "month" => date.ToString("MMM yyyy"),
            _ => date.ToString("MM/dd")
        };
    }

    public class AlbumStatsDTO
    {
        public int AlbumId { get; set; }
        public string AlbumName { get; set; }
        public int TotalShots { get; set; }
        public int ProcessedShots { get; set; }
        public int ShotsWithFaces { get; set; }
        public int ShotsWithoutFaces { get; set; }
        public int TotalFaces { get; set; }
        public int TotalComments { get; set; }
        public int CaptionedShots { get; set; }
        public DateTime? EarliestDate { get; set; }
        public DateTime? LatestDate { get; set; }
        public double ProcessedPercentage => TotalShots > 0 ? Math.Round(ProcessedShots * 100.0 / TotalShots, 1) : 0;
        public double CaptionedPercentage => TotalShots > 0 ? Math.Round(CaptionedShots * 100.0 / TotalShots, 1) : 0;
    }

    public class YearStatsDTO
    {
        public int Year { get; set; }
        public int TotalShots { get; set; }
        public int ProcessedShots { get; set; }
        public int ShotsWithFaces { get; set; }
        public int ShotsWithoutFaces { get; set; }
        public int TotalFaces { get; set; }
        public int TotalComments { get; set; }
        public int CaptionedShots { get; set; }
        public int AlbumCount { get; set; }
        public double ProcessedPercentage => TotalShots > 0 ? Math.Round(ProcessedShots * 100.0 / TotalShots, 1) : 0;
        public double CaptionedPercentage => TotalShots > 0 ? Math.Round(CaptionedShots * 100.0 / TotalShots, 1) : 0;
    }

    public class AdminStatsViewModel
    {
        public List<AlbumStatsDTO> AlbumStats { get; set; }
        public List<YearStatsDTO> YearStats { get; set; }
    }

    public class ProcessingHistogramViewModel
    {
        public string Period { get; set; }
        public int Days { get; set; }
        public List<string> Labels { get; set; }
        public List<int> FaceDetectionCounts { get; set; }
        public List<int> CaptionCounts { get; set; }
    }

}


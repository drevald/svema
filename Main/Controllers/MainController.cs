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
        return View(dto);
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
    [HttpPost("api/faces/reassign/{faceId}")]
    public async Task<IActionResult> ReassignFace(int faceId, [FromBody] int newPersonId)
    {
        await personService.ReassignFaceAsync(faceId, newPersonId);
        return Ok();
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

        // Load image to get dimensions
        using var image = SixLabors.ImageSharp.Image.Load(shot.FullScreen);
        int imageWidth = image.Width;
        int imageHeight = image.Height;

        var faceDetections = await dbContext.FaceDetections
            .Include(fd => fd.Person)
            .Where(fd => fd.ShotId == shotId)
            .Select(fd => new
            {
                x = fd.X,
                y = fd.Y,
                width = fd.Width,
                height = fd.Height,
                imageWidth = imageWidth,
                imageHeight = imageHeight,
                personName = fd.Person != null ? (fd.Person.FirstName + " " + fd.Person.LastName).Trim() : null
            })
            .ToListAsync();

        return Ok(faceDetections);
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
    [HttpGet("persons")]
    public async Task<IActionResult> PersonsList(int page = 1, int pageSize = 24)
    {
        // Optimized query: only load what we need for display, excluding faces from shots marked as "no faces"
        var personsQuery = dbContext.Persons
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

        return View(persons);
    }

    [Authorize]
    [HttpGet("person/{id}")]
    public async Task<IActionResult> Person(int id)
    {
        var person = await dbContext.Persons
            .Include(p => p.FaceDetections)
            .ThenInclude(fd => fd.Shot)
            .FirstOrDefaultAsync(p => p.PersonId == id);

        if (person == null) return NotFound();

        // Filter out face detections from shots marked as "no faces"
        var validFaceDetections = person.FaceDetections
            .Where(fd => fd.Shot != null && !fd.Shot.NoFaces)
            .OrderBy(fd => fd.ShotId)
            .ToList();

        // Get all persons for reassignment dropdown
        var allPersons = await dbContext.Persons
            .Where(p => p.PersonId != id)
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToListAsync();

        ViewBag.Person = person;
        ViewBag.AllPersons = allPersons;
        ViewBag.FaceCount = validFaceDetections.Count;

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

            // Merge all selected persons into the first one
            var targetPersonId = selectedPersons[0];
            foreach (var personId in selectedPersons.Skip(1))
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
        var person = (await personService.GetAllPersonsAsync()).FirstOrDefault(p => p.PersonId == id);
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
            Persons = await personService.GetAllPersonsAsync()
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
        var person = await dbContext.Persons.FindAsync(personId);
        if (person == null) return NotFound();

        var shot = await dbContext.Shots.FindAsync(dto.ShotId);
        if (shot == null) return NotFound(new { message = "Shot not found" });

        await personService.SetProfilePhotoAsync(personId, dto.ShotId);

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

}


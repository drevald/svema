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
using Microsoft.AspNetCore.Authorization;
using Form;
using Data;
using Utils;
using Common;
using Services;

namespace Controllers;

public class MainController : BaseController
{
    public MainController(ApplicationDbContext dbContext, IConfiguration config) : base(dbContext, config)
    {
    }

    public List<string> GetCameraModels()
    {
        return albumService.GetCameraModels();
    }

    [Authorize]
    [HttpGet("")]
    public IActionResult Albums(AlbumsListDTO dto)
    {
        dto ??= new AlbumsListDTO();
        var model = BuildAlbumsListAsync(dto, onlyMine: false);
        return View(model);
    }

    [Authorize]
    [HttpPost("")]
    public IActionResult ReloadAlbums(AlbumsListDTO dto)
    {
        dto ??= new AlbumsListDTO();
        var model = BuildAlbumsListAsync(dto, onlyMine: false);
        return View("Albums", model);
    }

    [Authorize]
    [HttpGet("my")]
    public IActionResult MyAlbums(AlbumsListDTO dto)
    {
        dto ??= new AlbumsListDTO();
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
                    if (dto.EditLocation && shotsToChange.Any())
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
        var model = BuildAlbumsListAsync(dto, onlyMine: true);
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
        AlbumDTO dto = new AlbumDTO();
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
        dto.AlbumComments = album.AlbumComments ?? new List<AlbumComment>();
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
                North = dto.North,
                South = dto.South,
                West = dto.West,
                East = dto.East
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

        if (shotsToUpdate.Any())
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
        await albumService.DeleteAlbum(id);
        return Redirect("/my");
    }

    [Authorize]
    [HttpPost("view_album")]
    public IActionResult ReloadAlbum(AlbumDTO dto, string? refresh, string? comment, string text, int id, int commentId)    
    {
        if (comment != null)
        {
            commentService.AddComment(GetUsername(), text, id, commentId);
        }
        return RedirectToAction("ViewAlbum", new
        {
            Id = dto.AlbumId,
            Token = dto.Token,
            DateStart = dto.DateStart,
            DateEnd = dto.DateEnd,
            LocationId = dto.LocationId,
            North = dto.North,
            South = dto.South,
            West = dto.West,
            East = dto.East
        });
    }

    [Authorize]
    [HttpGet("view_album")]
    public IActionResult ViewAlbum(
    int id, string? token,
    double? north, double? south, double? west, double? east,
    DateTime? dateStart, DateTime? dateEnd, int? locationId)
    {
        AlbumDTO dto = new AlbumDTO();
        dto.North = north ?? dto.North;
        dto.South = south ?? dto.South;
        dto.East = east ?? dto.East;
        dto.West = west ?? dto.West;
        dto.Token = token;
        var currentUserId = GetUserId();
        var album = albumService.GetAuthorizedAlbum(id, GetUserId(), token);

        if (album == null)
        {
            return RedirectToAction("Albums");
        }

        dto.Shots = shotService.GetShotPreviews(id, dto.West, dto.East, dto.South, dto.North);

        dto.Placemarks = locationService.GetShotsForShots(dto.Shots);
        GeoRect rect = GeoRect.FromPlacemarks(dto.Placemarks, 0.1);
        dto.North = rect.North;
        dto.South = rect.South;
        dto.East = rect.East;
        dto.West = rect.West;
        dto.AlbumId = album.AlbumId;
        dto.Name = album.Name;
        dto.AlbumComments = album.AlbumComments ?? new List<AlbumComment>();
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
        var dto = new ShotDTO(shot! ?? new Shot());
        dto.AlbumName = album?.Name ?? "";
        dto.Locations = locationService.GetLocations();
        dto.Longitude = shot!.Longitude;
        dto.Latitude = shot.Latitude;
        dto.Zoom = shot.Zoom;
        dto.IsCover = album != null && shot.ShotId == album.PreviewId;
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
        var result = shotService.GetAllShots();
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
        var dto = new UploadedFilesDTO();
        dto.AlbumId = id;
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

        var files = dto.Files ?? new List<IFormFile>();
        long size = files.Sum(f => f.Length);
        dto.FileErrors = new Dictionary<string, string>();

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

        var album = albumService.GetAlbum(dto.AlbumId);
        if (album == null)
        {
            dto.ErrorMessage = "Album not found";
            return View(dto);
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
                    await ProcessShot(bytes, formFile.FileName, formFile.ContentType, shot, album, storage, dto.FileErrors);
                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
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
    public IActionResult AddLocation(int locationId)
    {
        Location location = new Location();
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
        ShotDTO dto = new ShotDTO(shot);
        dto.Locations = locationService.GetLocations();
        dto.Longitude = shot!.Longitude;
        dto.Latitude = shot.Latitude;
        dto.Zoom = shot.Zoom;
        dto.Token = token;
        dto.AlbumName = shot.Album.Name;
        dto.ShotComments = commentService.GetShotComments(id);
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
        var dto = new ProfileDTO();
        var user = userService.GetUserByUsername(GetUsername());
        dto.User = user;
        dto.Storages = user != null
            ? userService.GetStoragesForUser(user.UserId)
            : new List<ShotStorage>();
        return View(dto);
    }

    [Authorize]
    [HttpGet("edit_local_storage")]
    public IActionResult EditLocalStorage(int userId, int storageId)
    {
        var dto = new StorageDTO();
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
    [HttpPost("select_album")]
    public IActionResult SelectAlbum(AlbumDTO dto)
    {
        if (dto == null)
        {
            return RedirectToAction("Albums");
        }

        var username = GetUsername() ?? string.Empty;

        var shots = (dto.Shots ?? new List<ShotPreviewDTO>()).Where(s => s != null && s.IsChecked).ToList();

        var albums = albumService.GetAlbumCardsForUser(username, dto.AlbumId);

        var selectAlbumDTO = new SelectAlbumDTO
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

        var shotsList = (dto.Shots ?? new List<ShotPreviewDTO>())
            .Select(s => s.ShotId)
            .ToList();

        shotService.MoveShots(shotsList, dto.SourceAlbumId, dto.TargetAlbumId);
        albumService.UpdateAlbumPreview(dto.SourceAlbumId, shotsList, dto.TargetAlbumId);

        return Redirect("edit_album?id=" + dto.SourceAlbumId);
    }


    public IEnumerable<Album> SortAlbums(IEnumerable<Album> albums, SortBy sortBy, SortDirection direction)
    {
        if (albums == null) return Enumerable.Empty<Album>();

        Func<Album, object> keySelector = sortBy switch
        {
            SortBy.EarliestDate => a => a.Shots != null && a.Shots.Any() ? a.Shots.Min(s => s.DateStart) : DateTime.MinValue,
            SortBy.LeastLatitude => a => a.Shots != null && a.Shots.Any() ? a.Shots.Min(s => s.Latitude) : 0.0,
            SortBy.LeastLongitude => a => a.Shots != null && a.Shots.Any() ? a.Shots.Min(s => s.Longitude) : 0.0,
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
        var shots = shotService.GetSameDayShots(month, day, 1);
        return View(shots);
    }

}


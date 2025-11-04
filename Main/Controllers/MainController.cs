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
        return dbContext.Shots.Select(s => s.Name).ToList();
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
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} UPDATE_MY_ALBUMS [T{Environment.CurrentManagedThreadId}] START >>>>>> ");

        if (!string.IsNullOrEmpty(dto.LocationName) && save != null)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} UPDATE_MY_ALBUMS [T{Environment.CurrentManagedThreadId}] ADD LOCATION");
            dbContext.Locations.Add(new Location
            {
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Name = dto.LocationName,
                Zoom = dto.Zoom
            });
        }

        foreach (var a in dto.Albums)
        {
            if (a == null) continue;

            if (a.IsChecked)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} UPDATE_MY_ALBUMS [T{Environment.CurrentManagedThreadId}] START GETTING SHOTS FOR ALBUM " + a.AlbumId);
                var shotsToChange = dbContext.Shots
                    .Where(s => s.AlbumId == a.AlbumId)
                    .Select(s => s.ShotId)
                    .ToList();
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS END GETTING SHOTS FOR ALBUM " + a.AlbumId);
                if (save != null)
                {
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS START SAVE " + a.AlbumId);
                    if (dto.EditLocation && shotsToChange.Any())
                    {
                        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SAVE SHOTS");
                        int chunkSize = 1000;
                        var shotIds = shotsToChange.ToArray();
                        for (int i = 0; i < shotIds.Length; i += chunkSize)
                        {
                            var chunk = shotIds.Skip(i).Take(chunkSize).ToArray();
                            var idParams = string.Join(",", chunk);
                            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SET LAT = " + dto.Latitude);
                            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SET LON = " + dto.Longitude);
                            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS SET ZOOM = " + dto.Zoom);
                            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS START UPDATE " + idParams);
                            dbContext.Database.ExecuteSqlRaw(
                                $"UPDATE Shots SET Latitude = {{0}}, Longitude = {{1}}, Zoom = {{2}} WHERE Id IN ({idParams})",
                                dto.Latitude, dto.Longitude, dto.Zoom
                            );
                            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS END UPDATE");
                        }
                    }
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS START SAVE CHANGES");
                    dbContext.SaveChanges();
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS END SAVE CHANGES");
                }

                if (delete != null)
                {
                    await DeleteAlbum(a.AlbumId);
                }
            }
        }
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] UPDATE_MY_ALBUMS END ALBUMS <<<<<<<<<<<<< ");
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
        dto ??= new AlbumsListDTO();

        var username = GetUsername() ?? string.Empty;
        var filteredShots = ApplyShotFilters(dto, onlyMine);
        int count = filteredShots.Count();
        Console.WriteLine($"Count = {count}");
        var shotGroups = filteredShots
            .GroupBy(s => s.AlbumId)
            .Select(g => new
            {
                AlbumId = g.Key,
                Size = g.Count(),
                EarliestDate = g.Min(s => s.DateStart),
                LeastLatitude = g.Min(s => s.Latitude),
                LeastLongitude = g.Min(s => s.Longitude)
            })
            .ToList();

        var albumIds = shotGroups.Select(g => g.AlbumId).ToList();
        var albumCards = dbContext.Albums
            .Include(a => a.PreviewShot)
            .Where(a => albumIds.Contains(a.AlbumId))
            .Select(a => new AlbumCardDTO
            {
                AlbumId = a.AlbumId,
                Name = a.Name,
                PreviewId = a.PreviewId,
                PreviewFlip = a.PreviewShot != null && a.PreviewShot.Flip,
                PreviewRotate = a.PreviewShot != null ? a.PreviewShot.Rotate : 0
            })
            .ToList();

        var enriched = albumCards
            .Join(shotGroups,
                card => card.AlbumId,
                group => group.AlbumId,
                (card, group) => new
                {
                    Card = card,
                    group.Size,
                    group.EarliestDate,
                    group.LeastLatitude,
                    group.LeastLongitude
                });

        var sortTuple = (dto.SortBy, dto.SortDirection);

        var sorted = sortTuple switch
        {
            (SortBy.EarliestDate, SortDirection.Ascending) => enriched.OrderBy(e => e.EarliestDate),
            (SortBy.EarliestDate, SortDirection.Descending) => enriched.OrderByDescending(e => e.EarliestDate),
            (SortBy.LeastLatitude, SortDirection.Ascending) => enriched.OrderBy(e => e.LeastLatitude),
            (SortBy.LeastLatitude, SortDirection.Descending) => enriched.OrderByDescending(e => e.LeastLatitude),
            (SortBy.LeastLongitude, SortDirection.Ascending) => enriched.OrderBy(e => e.LeastLongitude),
            (SortBy.LeastLongitude, SortDirection.Descending) => enriched.OrderByDescending(e => e.LeastLongitude),
            (SortBy.ShotCount, SortDirection.Ascending) => enriched.OrderBy(e => e.Size),
            (SortBy.ShotCount, SortDirection.Descending) => enriched.OrderByDescending(e => e.Size),
            _ => enriched.OrderBy(e => e.EarliestDate)
        };

        var finalAlbumCards = sorted
            .Select(e =>
            {
                e.Card.Size = e.Size;
                return e.Card;
            })
            .ToList();

        if (onlyMine)
        {
            var emptyAlbums = dbContext.Albums
                .Where(a => a.User != null && a.User.Username == username)
                .Where(a => !dbContext.Shots.Any(s => s.AlbumId == a.AlbumId))
                .ToList();

            var emptyAlbumCards = emptyAlbums
                .Select(a => new AlbumCardDTO
                {
                    AlbumId = a.AlbumId,
                    Name = a.Name,
                    PreviewId = -1,
                    PreviewFlip = false,
                    PreviewRotate = 0
                })
                .ToList();

            finalAlbumCards.AddRange(emptyAlbumCards);
        }

        var cameras = dbContext.Shots.Select(s => s.CameraModel).Distinct().ToList();
        var locations = dbContext.Locations.OrderBy(l => l.Name).ToList();
        var placemarks = locationService.GetClusteredShotsWithLabels(username, onlyMine, dto.West, dto.East, dto.South, dto.North);
        var rect = GeoRect.FromPlacemarks(placemarks, 0.1);
        return new AlbumsListDTO
        {
            Albums = finalAlbumCards,
            Locations = locations,
            DateStart = dto.DateStart,
            DateEnd = dto.DateEnd,
            North = rect.North,
            South = rect.South,
            West = rect.West,
            East = rect.East,
            EditLocation = dto.EditLocation,
            Cameras = cameras,
            Placemarks = placemarks,
            SortByOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = SortBy.EarliestDate.ToString(), Text = "По дате" },
                    new SelectListItem { Value = SortBy.Name.ToString(), Text = "По названию" },
                    new SelectListItem { Value = SortBy.LeastLatitude.ToString(), Text = "По долготе" },
                    new SelectListItem { Value = SortBy.LeastLongitude.ToString(), Text = "По широте" },
                    new SelectListItem { Value = SortBy.ShotCount.ToString(), Text = "По размеру" }
                },
            SortDirectionOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = SortDirection.Descending.ToString(), Text = "По убыванию" },
                    new SelectListItem { Value = SortDirection.Ascending.ToString(), Text = "По возрастанию" }
                }
        };
    }

    public IQueryable<Shot> ApplyShotFilters(AlbumsListDTO dto, bool onlyMine)
    {
        if (dto == null) return dbContext.Shots.AsNoTracking().AsQueryable();

        var provider = CultureInfo.InvariantCulture;
        var query = dbContext.Shots.AsNoTracking().AsQueryable();

        Console.WriteLine($"Count A = {query.Count()}");

        if (!string.IsNullOrEmpty(dto.DateStart))
        {
            if (DateTime.TryParseExact(dto.DateStart, "yyyy", provider, DateTimeStyles.None, out var start))
            {
                query = query.Where(s => s.DateStart >= start);
            }
        }

        Console.WriteLine($"Count B = {query.Count()}");

        if (!string.IsNullOrEmpty(dto.DateEnd))
        {
            if (DateTime.TryParseExact(dto.DateEnd, "yyyy", provider, DateTimeStyles.None, out var end))
            {
                query = query.Where(s => s.DateEnd <= end);
            }
        }

        Console.WriteLine($"Count C = {query.Count()}");

        query = query.Where(s =>
            s.Latitude <= dto.North &&
            s.Latitude >= dto.South &&
            s.Longitude >= dto.West &&
            s.Longitude <= dto.East
        );

        Console.WriteLine($"Count D = {query.Count()}");

        if (!string.IsNullOrEmpty(dto.Camera))
        {
            query = query.Where(s => s.CameraModel == dto.Camera);
        }

        Console.WriteLine($"Count E = {query.Count()}");

        var username = GetUsername() ?? string.Empty;

        if (onlyMine)
        {
            query = query.Where(s => s.Album != null && s.Album.User != null && s.Album.User.Username == username);
            Console.WriteLine($"Count F = {query.Count()}");
        }
        else
        {
            query = query.Where(shot =>
                    (shot.Album != null && shot.Album.User != null && shot.Album.User.Username == username)
                    || dbContext.SharedUsers.Any(su =>
                        su.GuestUser != null && su.GuestUser.Username == username &&
                        su.HostUser != null && su.HostUser.UserId == (shot.Album != null && shot.Album.User != null ? shot.Album.User.UserId : -1))
                    || dbContext.SharedAlbums.Any(sa =>
                        sa.GuestUser != null && sa.GuestUser.Username == username &&
                        sa.Album != null && sa.Album.AlbumId == (shot.Album != null ? shot.Album.AlbumId : -1)));
            Console.WriteLine($"Count G = {query.Count()}");
        }



        return query;
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

        var album = dbContext.Albums
            .Include(a => a.AlbumComments)
            .FirstOrDefault(a => a.AlbumId == id && a.User.UserId == currentUserId);


        if (album == null)
        {
            return RedirectToAction("Albums");
        }

        dto.Shots = dbContext.Shots
            .Where(s => s.AlbumId == id && s.Longitude > dto.West && s.Longitude < dto.East && s.Latitude > dto.South && s.Latitude < dto.North)
            .OrderBy(s => s.ShotId)
            .Select(s => new ShotPreviewDTO
            {
                ShotId = s.ShotId,
                Name = s.Name,
                SourceUri = s.SourceUri,
                Flip = s.Flip,
                Rotate = s.Rotate
            })
            .ToList();

        dto.Placemarks = locationService.GetShotsForShots(dto.Shots);
        GeoRect rect = GeoRect.FromPlacemarks(dto.Placemarks, 0.1);
        dto.North = north ?? rect.North;
        dto.South = south ?? rect.South;
        dto.East = east ?? rect.East;
        dto.West = west ?? rect.West;
        dto.AlbumId = album.AlbumId;
        dto.Name = album.Name;
        dto.AlbumComments = album.AlbumComments ?? new List<AlbumComment>();
        dto.Locations = dbContext.Locations.OrderBy(l => l.Name).ToList();

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

        var storedAlbum = dbContext.Albums.Find(dto.AlbumId);
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
        storedAlbum.Name = dto.Name;

        // Get shots that need updates
        var shotsToUpdate = dto.Shots
            .Where(s => s != null && (s.IsChecked || s.Rotate != 0 || s.Flip))
            .Select(s => s.ShotId)
            .Where(id => id > 0)
            .ToList();

        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] FOUND {shotsToUpdate.Count} SHOTS TO UPDATE");

        if (shotsToUpdate.Any())
        {
            // Bulk update shots using ExecuteSqlRaw for better performance
            int chunkSize = 1000;
            var shotIds = shotsToUpdate.ToArray();

            for (int i = 0; i < shotIds.Length; i += chunkSize)
            {
                var chunk = shotIds.Skip(i).Take(chunkSize).ToArray();
                var idParams = string.Join(",", chunk);

                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] START UPDATE CHUNK {i / chunkSize + 1}");

                // Build dynamic SQL based on what needs to be updated
                var updateFields = new List<string>();
                var parameters = new List<object>();
                var paramIndex = 0;

                if (dto.Year < 0)
                {
                    updateFields.Add($"DateStart = {{{paramIndex++}}}, DateEnd = {{{paramIndex++}}}");
                    parameters.Add(DateTime.MinValue);
                    parameters.Add(DateTime.MinValue);
                }
                else
                {
                    if (DateTime.MinValue != dto.DateStart)
                    {
                        updateFields.Add($"DateStart = {{{paramIndex++}}}");
                        parameters.Add(dto.DateStart);
                    }
                    if (DateTime.MinValue != dto.DateEnd)
                    {
                        updateFields.Add($"DateEnd = {{{paramIndex++}}}");
                        parameters.Add(dto.DateEnd);
                    }
                }

                if (dto.Longitude != 0 && dto.Latitude != 0)
                {
                    updateFields.Add($"Latitude = {{{paramIndex++}}}, Longitude = {{{paramIndex++}}}, Zoom = {{{paramIndex++}}}");
                    parameters.Add(dto.Latitude);
                    parameters.Add(dto.Longitude);
                    parameters.Add(dto.Zoom);
                }

                // Handle flip and rotate updates
                var flipRotateUpdates = dto.Shots
                    .Where(s => s != null && shotsToUpdate.Contains(s.ShotId))
                    .GroupBy(s => new { s.Flip, s.Rotate })
                    .ToList();

                foreach (var group in flipRotateUpdates)
                {
                    var groupShotIds = group.Select(s => s.ShotId).ToArray();
                    var groupIdParams = string.Join(",", groupShotIds);

                    var groupUpdateFields = new List<string>(updateFields);
                    groupUpdateFields.Add($"Flip = {{{paramIndex++}}}, Rotate = {{{paramIndex++}}}");

                    var groupParameters = new List<object>(parameters);
                    groupParameters.Add(group.Key.Flip);
                    groupParameters.Add(group.Key.Rotate);

                    var sql = $"UPDATE Shots SET {string.Join(", ", groupUpdateFields)} WHERE Id IN ({groupIdParams})";

                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] EXECUTING SQL FOR {groupShotIds.Length} SHOTS");
                    dbContext.Database.ExecuteSqlRaw(sql, groupParameters.ToArray());
                }

                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] END UPDATE CHUNK {i / chunkSize + 1}");
            }
        }

        // Add location if specified
        if (!string.IsNullOrEmpty(dto.LocationName) && dto.Longitude != 0 && dto.Latitude != 0)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} STORE_ALBUM [T{Environment.CurrentManagedThreadId}] ADDING LOCATION");
            var location = new Location
            {
                Zoom = dto.Zoom,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Name = dto.LocationName
            };
            dbContext.Add(location);
        }

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
        var user = dbContext.Users.FirstOrDefault(u => u.Username == GetUsername());
        if (user == null)
        {
            return Unauthorized();
        }
        album.User = user;
        dbContext.Add(album);
        dbContext.SaveChanges();
        return Redirect("/");
    }

    [Authorize]
    [HttpGet("delete_album")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        var album = dbContext.Albums.Find(id);
        if (album == null)
            return NotFound();

        var shotInfos = dbContext.Shots
            .Where(s => s.AlbumId == id)
            .Select(s => new
            {
                s.SourceUri,
                s.Storage
            })
            .ToList();

        foreach (var shot in shotInfos)
        {
            await Storage.DeleteFileAsync(shot.Storage, shot.SourceUri);
        }

        dbContext.Shots.RemoveRange(dbContext.Shots.Where(s => s.AlbumId == id));
        dbContext.Albums.Remove(album);
        dbContext.SaveChanges();

        return Redirect("/my");
    }

    [Authorize]
    [HttpPost("view_album")]
    public IActionResult ReloadAlbum(AlbumDTO dto)
    {
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

        dto.Shots = dbContext.Shots
            .Where(s => s.AlbumId == id && s.Longitude > dto.West && s.Longitude < dto.East && s.Latitude > dto.South && s.Latitude < dto.North)
            .OrderBy(s => s.ShotId)
            .Select(s => new ShotPreviewDTO
            {
                ShotId = s.ShotId,
                Name = s.Name,
                SourceUri = s.SourceUri,
                Flip = s.Flip,
                Rotate = s.Rotate
            })
            .ToList();

        dto.Placemarks = locationService.GetShotsForShots(dto.Shots);
        GeoRect rect = GeoRect.FromPlacemarks(dto.Placemarks, 0.1);
        dto.North = rect.North;
        dto.South = rect.South;
        dto.East = rect.East;
        dto.West = rect.West;
        dto.AlbumId = album.AlbumId;
        dto.Name = album.Name;
        dto.AlbumComments = album.AlbumComments ?? new List<AlbumComment>();
        dto.Locations = dbContext.Locations.OrderBy(l => l.Name).ToList();

        return View(dto);

    }

    ///////////////////////////////////      SHOTS     ////////////////////////////////////

    [Authorize]
    [HttpGet("edit_shot")]
    public IActionResult EditShot(int id)
    {

        var currentUserId = GetUserId();

        var shot = dbContext.Shots
            .Include(s => s.Album)           // include navigation if you need Album info
            .ThenInclude(a => a.User)        // optional: if you need the user too
            .FirstOrDefault(s => s.ShotId == id && s.Album.User.UserId == currentUserId);

        if (shot == null) return RedirectToAction("Albums");

        var album = dbContext.Albums.Find(shot.AlbumId);
        var dto = new ShotDTO(shot! ?? new Shot());
        dto.AlbumName = album?.Name ?? "";
        dto.Locations = dbContext.Locations.OrderBy(l => l.Name).ToList();
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

        var shot = dbContext.Shots.Find(dto.ShotId);
        if (shot == null) return NotFound();

        var album = dbContext.Albums.Find(shot.AlbumId);
        shot.Name = dto.Name;
        shot.DateStart = dto.DateStart;
        shot.DateEnd = dto.DateEnd;
        shot.Longitude = dto.Longitude;
        shot.Latitude = dto.Latitude;
        shot.Zoom = dto.Zoom;
        if (dto.IsCover && album != null)
        {
            album.PreviewId = dto.ShotId;
        }
        if (!string.IsNullOrEmpty(dto.LocationName) && dto.Longitude != 0 && dto.Latitude != 0)
        {
            Location location = new Location
            {
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Zoom = dto.Zoom,
                Name = dto.LocationName
            };
            dbContext.Add(location);
        }
        dbContext.SaveChanges();
        return Redirect("edit_album?id=" + shot.AlbumId);
    }

    [Authorize]
    [HttpGet("shots")]
    public IActionResult GetShots()
    {
        var result = dbContext.Shots.ToList();
        return View();
    }

    [Authorize]
    [HttpGet("preview")]
    public async Task<IActionResult> Preview(int id, int? rotate, bool? flip)
    {
        Console.WriteLine("PREVIEW `" + id);

        var shot = await dbContext.Shots
            .Where(s => s.ShotId == id)
            .Select(s => new { s.Preview })
            .FirstOrDefaultAsync();

        if (shot?.Preview == null || shot.Preview.Length == 0)
            return NotFound();

        await using var stream = new MemoryStream(shot.Preview);

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
    public async Task<IActionResult> Shot(int id, int? rotate, bool? flip)
    {
        var result = await dbContext.Shots.FindAsync(id);
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

        var shot = dbContext.Shots
            .Include(s => s.ShotComments)
            .Include(s => s.Storage)
            .Include(s => s.Album)
                .ThenInclude(a => a.User)
            .FirstOrDefault(s =>
                s.ShotId == id &&
                (
                    // 1️⃣ Shot is owned by current user
                    s.Album.User.UserId == currentUserId ||

                    // 2️⃣ Album is shared directly to this user
                    dbContext.SharedAlbums.Any(sa =>
                        sa.AlbumId == s.Album.AlbumId &&
                        sa.GuestUserId == currentUserId
                    ) ||

                    // 3️⃣ Album belongs to a host user who shared their library
                    dbContext.SharedUsers.Any(su =>
                        su.HostUserId == s.Album.User.UserId &&
                        su.GuestUserId == currentUserId
                    ) ||

                    // 4️⃣ Shot (or album) is shared via public link
                    dbContext.SharedLinks.Any(sl =>
                        (
                            (sl.ResourceType == "shot" && sl.ResourceId == s.ShotId) ||
                            (sl.ResourceType == "album" && sl.ResourceId == s.Album.AlbumId)
                        ) &&
                        !sl.Revoked &&
                        (sl.ExpiresAt == null || sl.ExpiresAt > DateTime.UtcNow) &&
                        sl.Token == token
                    )
                )
            );

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

        var user = dbContext.Users.FirstOrDefault(u => u.Username == GetUsername());
        if (user == null)
        {
            dto.ErrorMessage = "User not found";
            return View(dto);
        }

        var storage = dbContext.ShotStorages.FirstOrDefault(s => s.User != null && s.User.UserId == user.UserId);
        if (storage == null)
        {
            dto.ErrorMessage = "No file storage available";
            return View(dto);
        }

        await dbContext.SaveChangesAsync();

        var album = await dbContext.Albums.FindAsync(dto.AlbumId);
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
        var location = dbContext.Locations.Find(locationId);
        if (location != null)
        {
            dbContext.Remove(location);
            dbContext.SaveChanges();
        }
        return Redirect("locations");
    }

    [Authorize]
    [HttpGet("add_location")]
    public IActionResult AddLocation(int locationId)
    {
        Location location = new Location();
        dbContext.Locations.Add(location);
        dbContext.SaveChanges();
        return Redirect("edit_location?LocationId=" + location.Id);
    }

    [Authorize]
    [HttpGet("edit_location")]
    public IActionResult EditLocation(int locationId)
    {
        var location = dbContext.Locations.Find(locationId);
        if (location == null) return RedirectToAction("Locations");
        return View(location);
    }

    [Authorize]
    [HttpPost("edit_location")]
    public IActionResult SaveLocation(Location location)
    {
        if (location == null) return BadRequest();
        dbContext.Update(location);
        dbContext.SaveChanges();
        return Redirect("locations");
    }

    [Authorize]
    [HttpGet("view_shot")]
    public IActionResult ViewShot(int id, string? token)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} START GETTING SHOT " + id);

        var currentUserId = GetUserId();

        var shot = dbContext.Shots
            .Include(s => s.ShotComments)
            .Include(s => s.Album)
                .ThenInclude(a => a.User)
            .FirstOrDefault(s =>
                s.ShotId == id &&
                (
                    // 1️⃣ Shot is owned by current user
                    s.Album.User.UserId == currentUserId ||

                    // 2️⃣ Album is shared directly to this user
                    dbContext.SharedAlbums.Any(sa =>
                        sa.AlbumId == s.Album.AlbumId &&
                        sa.GuestUserId == currentUserId
                    ) ||

                    // 3️⃣ Album belongs to a host user who shared their library
                    dbContext.SharedUsers.Any(su =>
                        su.HostUserId == s.Album.User.UserId &&
                        su.GuestUserId == currentUserId
                    ) ||

                    // 4️⃣ Shot (or album) is shared via public link
                    dbContext.SharedLinks.Any(sl =>
                        (
                            (sl.ResourceType == "shot" && sl.ResourceId == s.ShotId) ||
                            (sl.ResourceType == "album" && sl.ResourceId == s.Album.AlbumId)
                        ) &&
                        !sl.Revoked &&
                        (sl.ExpiresAt == null || sl.ExpiresAt > DateTime.UtcNow) &&
                        sl.Token == token
                    )
                )
            );

        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} END GETTING SHOT " + id);
        if (shot == null) return NotFound();
        ShotDTO dto = new ShotDTO(shot);
        dto.Locations = dbContext.Locations.OrderBy(l => l.Name).ToList();
        dto.Longitude = shot!.Longitude;
        dto.Latitude = shot.Latitude;
        dto.Zoom = shot.Zoom;
        dto.Token = token;
        dto.AlbumName = shot.Album.Name;
        dto.ShotComments = new List<ShotComment>();
        return View(dto);
    }

    [Authorize]
    [HttpGet("delete_shot")]
    public async Task<IActionResult> DeleteShot(int id)
    {
        var shot = dbContext.Shots.Where(s => s.ShotId == id).Include(s => s.Storage).FirstOrDefault();
        if (shot == null) return NotFound();

        var albumId = shot.AlbumId;
        await Storage.DeleteFile(shot);
        dbContext.Remove(shot);
        dbContext.SaveChanges();
        return Redirect("/edit_album?id=" + albumId);
    }

    [Authorize]
    [HttpGet("view_next_shot")]
    public IActionResult ViewNextShot(int id, string? token)
    {
        var shot = dbContext.Shots.Find(id);
        if (shot == null) return Redirect("/view_shot?id=" + id);

        // Get the next shot in the album
        var nextShot = dbContext.Shots
            .Where(s => s.AlbumId == shot.AlbumId && s.ShotId > shot.ShotId)
            .OrderBy(s => s.ShotId)
            .FirstOrDefault();

        if (nextShot != null)
        {
            return Redirect($"/view_shot?id={nextShot.ShotId}&token={token}");
        }

        // If no next shot, stay on current
        return Redirect($"/view_shot?id={id}&token={token}");
    }

    [Authorize]
    [HttpGet("view_prev_shot")]
    public IActionResult ViewPrevShot(int id, string? token)
    {
        var shot = dbContext.Shots.Find(id);
        if (shot == null) return Redirect("/view_shot?id=" + id);

        var prevShot = dbContext.Shots
        .Where(s => s.AlbumId == shot.AlbumId && s.ShotId < shot.ShotId)
        .OrderBy(s => s.ShotId)
        .LastOrDefault();

        if (prevShot != null)
        {
            return Redirect($"/view_shot?id={prevShot.ShotId}&token={token}");
        }

        return Redirect($"/view_shot?id={id}&token={token}");
    }

    [Authorize]
    [HttpPost("add_comment")]
    public IActionResult AddComment(string text, int id, int commentId)
    {
        var user = dbContext.Users.FirstOrDefault(u => u.Username == GetUsername());
        if (user == null) return Unauthorized();

        if (commentId == 0)
        {
            var comment = new AlbumComment
            {
                Author = user,
                AuthorId = user.UserId,
                AuthorUsername = user.Username,
                Text = text,
                AlbumId = id,
                Timestamp = DateTime.Now
            };
            dbContext.AlbumComments.Add(comment);
        }
        else
        {
            var comment = dbContext.AlbumComments.Find(commentId);
            if (comment == null) return NotFound();
            comment.Text = text;
            comment.AlbumId = id;
            comment.Timestamp = DateTime.Now;
            dbContext.AlbumComments.Update(comment);
        }
        dbContext.SaveChanges();
        return Redirect("view_album?id=" + id);
    }

    [Authorize]
    [HttpGet("delete_comment")]
    public IActionResult DeleteComment(int commentId, int id)
    {
        var comment = dbContext.AlbumComments.Find(commentId);
        if (comment != null)
        {
            dbContext.AlbumComments.Remove(comment);
            dbContext.SaveChanges();
        }
        return Redirect("view_album?id=" + id);
    }

    [Authorize]
    [HttpPost("add_shot_comment")]
    public IActionResult AddShotComment(string text, int id, int commentId)
    {
        var user = dbContext.Users.FirstOrDefault(u => u.Username == GetUsername());
        if (user == null) return Unauthorized();

        if (commentId == 0)
        {
            var comment = new ShotComment
            {
                Author = user,
                AuthorId = user.UserId,
                AuthorUsername = user.Username,
                Text = text,
                ShotId = id,
                Timestamp = DateTime.Now
            };
            dbContext.ShotComments.Add(comment);
        }
        else
        {
            var comment = dbContext.ShotComments.Find(commentId);
            if (comment == null) return NotFound();
            comment.Text = text;
            comment.ShotId = id;
            comment.Timestamp = DateTime.Now;
            dbContext.ShotComments.Update(comment);
        }
        dbContext.SaveChanges();
        return Redirect("view_shot?id=" + id);
    }

    [Authorize]
    [HttpGet("delete_shot_comment")]
    public IActionResult DeleteShotComment(int commentId, int id)
    {
        var comment = dbContext.ShotComments.Find(commentId);
        if (comment != null)
        {
            dbContext.ShotComments.Remove(comment);
            dbContext.SaveChanges();
        }
        return Redirect("view_shot?id=" + id);
    }

    [Authorize]
    [HttpGet("locations")]
    public IActionResult Locations()
    {
        var locations = dbContext.Locations.OrderBy(l => l.Name).ToList();
        return View(locations);
    }

    [Authorize]
    [HttpGet("profile")]
    public IActionResult Profile()
    {
        var dto = new ProfileDTO();
        var user = dbContext.Users.FirstOrDefault(u => u.Username == GetUsername());
        dto.User = user;
        dto.Storages = user != null
            ? dbContext.ShotStorages.Where(s => s.User != null && s.User.UserId == user.UserId).ToList()
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
            dto.Storage = dbContext.ShotStorages.Where(s => s.Id == storageId).FirstOrDefault() ?? new ShotStorage();
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
        dbContext.AddOrUpdateEntity(dto.Storage);
        dbContext.SaveChanges();
        return Redirect("profile?user_id=" + dto.Storage.UserId);
    }

    [Authorize]
    [HttpPost("select_album")]
    public async Task<IActionResult> SelectAlbum(AlbumDTO dto)
    {
        if (dto == null)
        {
            return RedirectToAction("Albums");
        }

        var username = GetUsername() ?? string.Empty;

        var shots = (dto.Shots ?? new List<ShotPreviewDTO>()).Where(s => s != null && s.IsChecked).ToList();

        var albums = await dbContext.Albums
            .Where(a => a.User != null && a.User.Username == username && a.AlbumId != dto.AlbumId)
            .GroupJoin(
                dbContext.Shots,
                album => album.PreviewId,
                shot => shot.ShotId,
                (album, shots) => new { Album = album, Shots = shots }
            )
            .SelectMany(
                x => x.Shots.DefaultIfEmpty(),
                (x, shot) => new AlbumCardDTO
                {
                    AlbumId = x.Album.AlbumId,
                    Name = x.Album.Name,
                    PreviewId = x.Album.PreviewId,
                    PreviewFlip = shot != null ? shot.Flip : false,
                    PreviewRotate = shot != null ? shot.Rotate : 0
                }
            )
            .OrderBy(a => a.Name)
            .ToListAsync();

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

        var sourceAlbum = dbContext.Albums
            .FirstOrDefault(a => a.AlbumId == dto.SourceAlbumId);
        if (sourceAlbum == null) return RedirectToAction("Albums");

        var shotsList = (dto.Shots ?? new List<ShotPreviewDTO>())
            .Select(s => s.ShotId)
            .ToList();

        var shots = dbContext.Shots
            .Where(s => s.AlbumId == dto.SourceAlbumId && shotsList.Contains(s.ShotId))
            .ToList();

        // Move shots
        foreach (var shot in shots)
            shot.AlbumId = dto.TargetAlbumId;

        // Update preview of source album if its current preview was moved
        if (shotsList.Contains(sourceAlbum.PreviewId))
        {
            var newPreviewId = dbContext.Shots
                .Where(s => s.AlbumId == dto.SourceAlbumId && !shotsList.Contains(s.ShotId))
                .Select(s => s.ShotId)
                .FirstOrDefault();

            // If nothing left — set to 0
            sourceAlbum.PreviewId = newPreviewId != 0 ? newPreviewId : 0;
        }

        // Ensure target album has a preview
        var targetAlbum = dbContext.Albums
            .FirstOrDefault(a => a.AlbumId == dto.TargetAlbumId);

        if (targetAlbum != null && targetAlbum.PreviewId == 0)
        {
            targetAlbum.PreviewId = shotsList[0];
        }

        dbContext.SaveChanges();

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
}


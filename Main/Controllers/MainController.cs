using System;
using System.Collections.Generic;
using System.Globalization;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Form;
using Data;
using Utils;
using Npgsql;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;

namespace Controllers;

public class MainController : BaseController
{

    public MainController(ApplicationDbContext dbContext, IConfiguration config) : base(dbContext, config)
    {
    }
    public List<string> GetCameraModels()
    {
        // return dbContext.Shots
        //     .AsNoTracking()
        //     .Select(s => s.CameraModel)
        //     .Where(model => model != null && model != "") // optional: filter out empty/null
        //     .Distinct()
        //     .OrderBy(m => m)
        //     .ToList();

        return dbContext.Shots.Select(s => s.Name).ToList();


    }

    // return dbContext.Shots
    //     .Select(s => s.CameraModel)
    //     .Where(model => model != null)
    //     .Distinct()
    //     .OrderBy(model => model) // Optional: to get sorted results
    //     .ToList();
    //}


    //To get clustered view of locations on map
    public List<LocationDTO> GetClusteredShotsWithLabels(double longitudeMin, double longitudeMax, double latitudeMin, double latitudeMax)
    {

        string sql = @"
        SELECT 
            COUNT(*) AS count,
            ST_X(ST_Centroid(ST_Collect(geom))) AS lon,
            ST_Y(ST_Centroid(ST_Collect(geom))) AS lat
        FROM (
            SELECT ST_SnapToGrid(
                     ST_SetSRID(ST_MakePoint(longitude, latitude), 4326), @gridSize
                   ) AS tile_geom,
                   ST_SetSRID(ST_MakePoint(longitude, latitude), 4326) AS geom
            FROM shots
            WHERE longitude BETWEEN @longitudeMin AND @longitudeMax
              AND latitude BETWEEN @latitudeMin AND @latitudeMax
        ) AS clustered
        GROUP BY tile_geom;";

        // Create the list to store the results
        var locationList = new List<LocationDTO>();

        // Open the database connection
        using (var connection = dbContext.Database.GetDbConnection())
        {

            connection.Open();

            // Create the SQL command
            using (var command = connection.CreateCommand())
            {

                command.CommandText = sql;

                // Add parameters to the command
                command.Parameters.Add(new NpgsqlParameter("@gridSize", (longitudeMax - longitudeMin) / 10));
                command.Parameters.Add(new NpgsqlParameter("@longitudeMin", longitudeMin));
                command.Parameters.Add(new NpgsqlParameter("@longitudeMax", longitudeMax));
                command.Parameters.Add(new NpgsqlParameter("@latitudeMin", latitudeMin));
                command.Parameters.Add(new NpgsqlParameter("@latitudeMax", latitudeMax));

                // Execute the query and process the results
                using (var reader = command.ExecuteReader())
                {

                    while (reader.Read())
                    {

                        var location = new LocationDTO
                        {
                            Label = reader.GetInt32(reader.GetOrdinal("count")).ToString(), // Assuming Label is count as string
                            Longitude = reader.GetDouble(reader.GetOrdinal("lon")), // Longitude of the centroid
                            Latitude = reader.GetDouble(reader.GetOrdinal("lat")) // Latitude of the centroid
                        };
                        locationList.Add(location);
                    }
                }
            }
        }
        return locationList;
    }

    [Authorize]
    [HttpGet("")]
    public async Task<IActionResult> Albums(AlbumsListDTO dto)
    {
        var model = await BuildAlbumsListAsync(dto, onlyMine: false);
        return View(model);
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> MyAlbums(AlbumsListDTO dto)
    {      
        // Fetch the album list including empty albums
        var model = await BuildAlbumsListAsync(dto, onlyMine: true);

        // var username = HttpContext.User.FindFirst("user")?.Value;
        // var emptyAlbums = await dbContext.Albums
        //     .Where(a => a.User.Username == username)
        //     .Where(a => !dbContext.Shots.Any(s => s.AlbumId == a.AlbumId))
        //     .Select(a => new AlbumCardDTO
        //     {
        //         AlbumId = a.AlbumId,
        //         Name = a.Name,
        //         Size = 0  // Empty album size
        //     })
        //     .ToListAsync();

        // // Concatenate empty albums to the existing album list
        // model.Albums = model.Albums.Concat(emptyAlbums).ToList();

        return View(model);
    }

    protected async Task<AlbumsListDTO> BuildAlbumsListAsync(AlbumsListDTO dto, bool onlyMine)
    {
        var filteredShots = ApplyShotFilters(dto, onlyMine);

        var albumCards = await filteredShots
            .GroupBy(s => s.AlbumId)
            .Select(g => new
            {
                AlbumId = g.Key,
                Size = g.Count()
            })
            .Join(dbContext.Albums,
                grouped => grouped.AlbumId,
                album => album.AlbumId,
                (grouped, album) => new AlbumCardDTO
                {
                    AlbumId = album.AlbumId,
                    Name = album.Name,
                    Size = grouped.Size,
                    PreviewId = album.PreviewId,
                    PreviewFlip = dbContext.Shots
                        .Where(ps => ps.ShotId == album.PreviewId)
                        .Select(ps => ps.Flip)
                        .FirstOrDefault(),
                    PreviewRotate = dbContext.Shots
                        .Where(ps => ps.ShotId == album.PreviewId)
                        .Select(ps => ps.Rotate)
                        .FirstOrDefault()
                })
            .OrderBy(a => a.Name)
            .ToListAsync();

        return new AlbumsListDTO
        {
            Albums = albumCards,
            Locations = await dbContext.Locations.ToListAsync(),
            DateStart = dto.DateStart,
            DateEnd = dto.DateEnd,
            North = dto.North,
            South = dto.South,
            West = dto.West,
            East = dto.East,
            Cameras = dbContext.Shots.Select(s => s.CameraModel).Distinct().ToList(),
            Placemarks = GetClusteredShotsWithLabels(dto.West, dto.East, dto.South, dto.North)
        };
    }

    public IQueryable<Shot> ApplyShotFilters(AlbumsListDTO dto, bool onlyMine)
    {
        var provider = CultureInfo.InvariantCulture;
        var query = dbContext.Shots.AsQueryable();

        if (!string.IsNullOrEmpty(dto.DateStart))
        {
            var start = DateTime.ParseExact(dto.DateStart, "yyyy", provider);
            query = query.Where(s => s.DateStart >= start);
        }

        if (!string.IsNullOrEmpty(dto.DateEnd))
        {
            var end = DateTime.ParseExact(dto.DateEnd, "yyyy", provider);
            query = query.Where(s => s.DateEnd <= end);
        }

        query = query.Where(s =>
            s.Latitude <= dto.North &&
            s.Latitude >= dto.South &&
            s.Longitude >= dto.West &&
            s.Longitude <= dto.East
        );

        if (!string.IsNullOrEmpty(dto.Camera))
        {
            query = query.Where(s => s.CameraModel == dto.Camera);
        }

        var username = HttpContext.User.FindFirst("user")?.Value;

        if (onlyMine)
        {
            query = query.Where(s => s.Album.User.Username == username);
        }
        else
        {
            query = query.Where(shot =>
                    shot.Album.User.Username == username
                    || dbContext.SharedUsers.Any(su =>
                        su.GuestUser.Username == username &&
                        su.HostUser.UserId == shot.Album.User.UserId)
                    || dbContext.SharedAlbums.Any(sa =>
                        sa.GuestUser.Username == username &&
                        sa.Album.AlbumId == shot.Album.AlbumId));
        }

        return query;
    }


    ///////////////////   ALBUM  /////////////////////////////////////////

    [HttpGet("edit_album")]
    public async Task<IActionResult> EditAlbum(int id)
    {
        AlbumDTO dto = new AlbumDTO();
        var album = await dbContext.Albums.Include(a => a.AlbumComments).Where(a => a.AlbumId == id).FirstAsync();

        //var shots = await dbContext.Shots.Where(s => s.Album.AlbumId == id).ToListAsync();
        dto.Shots = await dbContext.Shots
            .Where(s => s.AlbumId == id)
            .OrderBy(s => s.ShotId)
            .Select(s => new ShotPreviewDTO
            {
                ShotId = s.ShotId,
                Name = s.Name,
                SourceUri = s.SourceUri,
                Flip = s.Flip,
                Rotate = s.Rotate
            })
            .ToListAsync();

        dto.AlbumId = album.AlbumId;
        dto.Name = album.Name;
        dto.AlbumComments = album.AlbumComments;
        dto.Locations = await dbContext.Locations.ToListAsync();

        return View(dto);

    }

    [HttpPost("edit_album")]
    public async Task<IActionResult> StoreAlbum(AlbumDTO dto)
    {
        if (dto == null)
        {
            Console.WriteLine("DTO is null");
            return BadRequest();
        }

        var storedAlbum = await dbContext.Albums.FindAsync(dto.AlbumId);
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

        storedAlbum.Name = dto.Name;

        var shotIds = dto.Shots.Select(s => s.ShotId).ToList();
        if (!shotIds.Any())
        {
            Console.WriteLine($"No shots to update for album {dto.AlbumId}");
        }

        var shots = await dbContext.Shots
            .Where(s => shotIds.Contains(s.ShotId))
            .ToListAsync();

        var shotsDict = shots.ToDictionary(s => s.ShotId);

        foreach (var s in dto.Shots)
        {
            if (s == null)
                continue;

            if (!shotsDict.TryGetValue(s.ShotId, out var shot))
            {
                Console.WriteLine($"Shot {s.ShotId} not found in database, skipping");
                continue;
            }

            if (s.IsChecked || s.Rotate != shot.Rotate || s.Flip != shot.Flip)
            {
                if (dto.Year < 0)
                {
                    shot.DateStart = DateTime.MinValue;
                    shot.DateEnd = DateTime.MinValue;
                }
                if (DateTime.MinValue != dto.DateStart)
                {
                    shot.DateStart = dto.DateStart;
                }
                if (DateTime.MinValue != dto.DateEnd)
                {
                    shot.DateEnd = dto.DateEnd;
                }
                if (dto.Longitude != 0 && dto.Latitude != 0)
                {
                    shot.Latitude = dto.Latitude;
                    shot.Longitude = dto.Longitude;
                    shot.Zoom = dto.Zoom;
                }
                shot.Flip = s.Flip;
                shot.Rotate = s.Rotate;
            }
        }

        if (!string.IsNullOrEmpty(dto.LocationName) && dto.Longitude != 0 && dto.Latitude != 0)
        {
            var location = new Location
            {
                Zoom = dto.Zoom,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Name = dto.LocationName
            };
            dbContext.Add(location);
        }

        Console.WriteLine($"STORING ALBUM ({dto.AlbumId})");
        await dbContext.SaveChangesAsync();

        return Redirect("/my");
    }


    [HttpGet("add_album")]
    public IActionResult AddAlbum()
    {
        return View();
    }

    [HttpPost("add_album")]
    public async Task<IActionResult> CreateAlbum(Album album)
    {
        Console.Write("STORING ALBUM (" + album.AlbumId + ")");
        User user = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).First();
        album.User = user;
        dbContext.Add(album);
        await dbContext.SaveChangesAsync();
        return Redirect("/");
    }

    [HttpGet("delete_album")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        var album = await dbContext.Albums.FindAsync(id);
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
            Storage.DeleteFile(shot.Storage, shot.SourceUri);
        }

        dbContext.Shots.RemoveRange(dbContext.Shots.Where(s => s.AlbumId == id));
        dbContext.Albums.Remove(album);
        await dbContext.SaveChangesAsync();

        return Redirect("/my");
    }


    [HttpGet("view_album")]
    public async Task<IActionResult> ViewAlbum(int id)
    {
        var album = await dbContext.Albums.Include(a => a.AlbumComments).Where(a => a.AlbumId == id).FirstAsync();
        var shots = await dbContext.Shots.Where(s => s.Album.AlbumId == id).OrderBy(s => s.ShotId).ToListAsync();
        var locations = new HashSet<Location>();
        ViewBag.locations = locations;
        ViewBag.album = album;
        ViewBag.shots = shots;
        ViewBag.albumId = id;
        return View(album);
    }

    ///////////////////////////////////      SHOTS     ////////////////////////////////////

    [HttpGet("edit_shot")]
    public async Task<IActionResult> EditShot(int id)
    {
        var shot = await dbContext.Shots.FindAsync(id);
        var album = await dbContext.Albums.FindAsync(shot.AlbumId);
        ShotDTO dto = new ShotDTO(shot);
        var locations = await dbContext.Locations.ToListAsync();
        dto.Locations = locations;
        dto.Longitude = shot.Longitude;
        dto.Latitude = shot.Latitude;
        dto.Zoom = shot.Zoom;
        dto.IsCover = shot.ShotId == album.PreviewId;
        return View(dto);
    }

    [HttpPost("edit_shot")]
    public async Task<IActionResult> StoreShot(ShotDTO dto)
    {
        Shot shot = await dbContext.Shots.FindAsync(dto.ShotId);
        Album album = await dbContext.Albums.FindAsync(shot.AlbumId);
        shot.Name = dto.Name;
        shot.DateStart = dto.DateStart;
        shot.DateEnd = dto.DateEnd;
        shot.Longitude = dto.Longitude;
        shot.Latitude = dto.Latitude;
        shot.Zoom = dto.Zoom;
        if (dto.IsCover)
        {
            album.PreviewId = dto.ShotId;
        }
        if (dto.LocationName != null && dto.Longitude != 0 && dto.Latitude != 0)
        {
            Location location = new Location();
            location.Latitude = dto.Latitude;
            location.Longitude = dto.Longitude;
            location.Zoom = dto.Zoom;
            location.Name = dto.LocationName;
            dbContext.Add(location);
        }
        await dbContext.SaveChangesAsync();
        return Redirect("edit_album?id=" + shot.AlbumId);
    }

    [HttpGet("shots")]
    public async Task<IActionResult> GetShots()
    {
        var result = await dbContext.Shots.ToListAsync();
        return View();
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(int id, int? rotate, bool? flip)
    {
        Console.WriteLine("PREVIEW `" + id);
        var result = await dbContext.Shots.FindAsync(id);
        Console.WriteLine("PREVIEW result is " + result);

        var stream = new MemoryStream();
        stream.Write(result.Preview, 0, result.Preview.Length);
        stream.Position = 0;

        // Set the correct MIME type for JPEG
        string mimeType = "image/jpeg";
        if (result.Preview.Length > 0)
        {
            // Check if the file extension is correct (or if flip/rotate is needed)
            if (rotate.HasValue || flip.HasValue)
            {
                // Apply rotation or flip as needed (use your transformation function here)
                var transformedStream = ImageUtils.GetTransformedImage(stream, rotate ?? 0, flip ?? false);
                return new FileStreamResult(transformedStream, mimeType);  // Return the transformed image with proper MIME type
            }
        }

        // Return the original stream if no transformation is needed
        return new FileStreamResult(stream, mimeType);  // Ensure the MIME type is correctly set
    }

    [HttpGet("shot")]
    public IActionResult Shot(int id)
    {
        var shot = dbContext.Shots.Where(s => s.ShotId == id).Include(s => s.Storage).FirstOrDefault();
        if (shot == null || shot.FullScreen == null)
        {
            return NotFound(); // Return 404 if shot is not found or fullscreen is null
        }
        return File(shot.FullScreen, shot.ContentType); // Return the byte array with the correct content type
    }

    [HttpGet("orig")]
    public IActionResult Orig(int id)
    {
        var shot = dbContext.Shots.Where(s => s.ShotId == id).Include(s => s.Storage).FirstOrDefault();
        if (shot == null || shot.FullScreen == null)
        {
            return NotFound();
        }
        Stream stream = Storage.GetFile(shot);
        return File(stream, shot.ContentType);
    }

    [HttpGet("upload_shots")]
    public IActionResult UploadFile(int id)
    {
        var dto = new UploadedFilesDTO();
        dto.AlbumId = id;
        return View(dto);
    }

    [RequestSizeLimit(1000_000_000)]
    [HttpPost("upload_shots")]
    public async Task<IActionResult> StoreFile(UploadedFilesDTO dto)
    {
        User user = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).First();
        ShotStorage storage = dbContext.ShotStorages.Where(s => s.User == user).FirstOrDefault(s => true);
        if (storage == null)
        {
            dto.ErrorMessage = "No file storage available";
            return View(dto);
        }
        await dbContext.SaveChangesAsync();
        Album album = await dbContext.Albums.FindAsync(dto.AlbumId);
        long size = dto.Files.Sum(f => f.Length);
        var filePaths = new List<string>();
        dto.FileErrors = new Dictionary<string, string>();
        foreach (var formFile in dto.Files)
        {
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

    [HttpGet("delete_location")]
    public async Task<IActionResult> DeleteLocation(int locationId)
    {
        Location location = await dbContext.Locations.FindAsync(locationId);
        dbContext.Remove(location);
        await dbContext.SaveChangesAsync();
        return Redirect("locations");
    }

    [HttpGet("add_location")]
    public async Task<IActionResult> AddLocation(int locationId)
    {
        Location location = new Location();
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync();
        //return EditLocation(location.Id);
        return Redirect("edit_location?LocationId=" + location.Id);
    }

    [HttpGet("edit_location")]
    public async Task<IActionResult> EditLocation(int locationId)
    {
        Location location = await dbContext.Locations.FindAsync(locationId);
        return View(location);
    }

    [HttpPost("edit_location")]
    public async Task<IActionResult> SaveLocation(Location location)
    {
        dbContext.Update(location);
        await dbContext.SaveChangesAsync();
        return Redirect("locations");
    }

    [HttpGet("view_shot")]
    public async Task<IActionResult> ViewShot(int id)
    {
        var shot = await dbContext.Shots
            .Include(s => s.ShotComments)
            .Include(s => s.Album)
            .FirstOrDefaultAsync(s => s.ShotId == id);
        return View(shot);
    }

    [HttpGet("delete_shot")]
    public async Task<IActionResult> DeleteShot(int id)
    {
        Shot shot = dbContext.Shots.Where(s => s.ShotId == id).Include(s => s.Storage).First();
        var albumId = shot.AlbumId;
        Storage.DeleteFile(shot);
        dbContext.Remove(shot);
        await dbContext.SaveChangesAsync();
        return Redirect("/edit_album?id=" + albumId);
    }

    [HttpGet("view_next_shot")]
    public async Task<IActionResult> ViewNextShot(int id)
    {
        var shot = await dbContext.Shots.FindAsync(id);
        var shots = dbContext.Shots.Where(s => s.AlbumId == shot.AlbumId).ToList<Shot>();
        int index = shots.FindIndex(a => a.ShotId == id);
        try
        {
            return Redirect("/view_shot?id=" + shots[index + 1].ShotId);
        }
        catch (Exception)
        {
            return Redirect("/view_shot?id=" + id);
        }
    }

    [HttpGet("view_prev_shot")]
    public async Task<IActionResult> ViewPrevShot(int id)
    {
        var shot = await dbContext.Shots.FindAsync(id);
        var shots = dbContext.Shots.Where(s => s.AlbumId == shot.AlbumId).ToList<Shot>();
        int index = shots.FindIndex(a => a.ShotId == id);
        try
        {
            return Redirect("/view_shot?id=" + shots[index - 1].ShotId);
        }
        catch (Exception)
        {
            return Redirect("/view_shot?id=" + id);
        }
    }

    [HttpPost("add_comment")]
    public async Task<IActionResult> AddComment(string text, int id, int commentId)
    {
        var comment = new AlbumComment();
        User user = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).First();
        if (commentId == 0)
        {
            comment.Author = user;
            comment.AuthorId = user.UserId;
            comment.AuthorUsername = user.Username;
            comment.Text = text;
            comment.AlbumId = id;
            comment.Timestamp = DateTime.Now;
            dbContext.AlbumComments.Add(comment);
        }
        else
        {
            comment = await dbContext.AlbumComments.FindAsync(commentId);
            comment.Text = text;
            comment.AlbumId = id;
            comment.Timestamp = DateTime.Now;
            dbContext.AlbumComments.Update(comment);
        }
        await dbContext.SaveChangesAsync();
        return Redirect("view_album?id=" + id);
    }

    [HttpGet("delete_comment")]
    public async Task<IActionResult> DeleteComment(int commentId, int id)
    {
        var comment = await dbContext.AlbumComments.FindAsync(commentId);
        dbContext.AlbumComments.Remove(comment);
        await dbContext.SaveChangesAsync();
        return Redirect("view_album?id=" + id);
    }

    [HttpPost("add_shot_comment")]
    public async Task<IActionResult> AddShotComment(string text, int id, int commentId)
    {
        var comment = new ShotComment();
        User user = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).First();
        if (commentId == 0)
        {
            comment.Author = user;
            comment.AuthorId = user.UserId;
            comment.AuthorUsername = user.Username;
            comment.Text = text;
            comment.ShotId = id;
            comment.Timestamp = DateTime.Now;
            dbContext.ShotComments.Add(comment);
        }
        else
        {
            comment = await dbContext.ShotComments.FindAsync(commentId);
            comment.Text = text;
            comment.ShotId = id;
            comment.Timestamp = DateTime.Now;
            dbContext.ShotComments.Update(comment);
        }
        await dbContext.SaveChangesAsync();
        return Redirect("view_shot?id=" + id);
    }

    [HttpGet("delete_shot_comment")]
    public async Task<IActionResult> DeleteShotComment(int commentId, int id)
    {
        var comment = await dbContext.ShotComments.FindAsync(commentId);
        dbContext.ShotComments.Remove(comment);
        await dbContext.SaveChangesAsync();
        return Redirect("view_shot?id=" + id);
    }

    [HttpGet("locations")]
    public async Task<IActionResult> Locations()
    {
        var locations = await dbContext.Locations.ToListAsync<Location>();
        return View(locations);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var dto = new ProfileDTO();
        dto.User = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).FirstOrDefault(e => true);
        dto.Storages = await dbContext.ShotStorages.Where(s => s.User == dto.User).ToListAsync<ShotStorage>();
        return View(dto);
    }

    [HttpGet("edit_local_storage")]
    public IActionResult EditLocalStorage(int userId, int storageId)
    {
        var dto = new StorageDTO();
        if (storageId != 0)
        {
            dto.Storage = dbContext.ShotStorages.Where(s => s.Id == storageId).First();
        }
        else
        {
            dto.Storage = new ShotStorage();
            dto.Storage.Root = "/storage";
            dto.Storage.Provider = Provider.Local;
            dto.Storage.UserId = userId;
        }
        return View(dto);
    }

    [HttpPost("edit_local_storage")]
    public async Task<IActionResult> SaveLocalStorage(StorageDTO dto)
    {
        dbContext.AddOrUpdateEntity(dto.Storage);
        await dbContext.SaveChangesAsync();
        return Redirect("profile?user_id=" + dto.Storage.UserId);
    }

    [HttpPost("select_album")]
    public async Task<IActionResult> SelectAlbum(AlbumDTO dto)
    {
        var username = HttpContext.User.FindFirst("user")?.Value;

        var shots = dto.Shots.Where(s => s.IsChecked).ToList();

        var albums = await dbContext.Albums
            .Where(a => a.User.Username == username)
            .Join(dbContext.Shots,
                album => album.PreviewId,
                shot => shot.ShotId,
                (album, shot) => new AlbumCardDTO
                {
                    AlbumId = album.AlbumId,
                    Name = album.Name,
                    PreviewId = album.PreviewId,
                    PreviewFlip = shot.Flip,
                    PreviewRotate = shot.Rotate
                })
            .ToListAsync();

        var selectAlbumDTO = new SelectAlbumDTO
        {
            Shots = shots,
            Albums = albums,
            SourceAlbumId = dto.AlbumId
        };

        return View(selectAlbumDTO);
    }

    [HttpPost("move_shots")]
    public async Task<IActionResult> MoveShots(SelectAlbumDTO dto)
    {
        var sourceAlbum = await dbContext.Albums
            .Where(a => a.AlbumId == dto.SourceAlbumId)
            .FirstAsync();

        var shotsList = dto.Shots.Select(s => s.ShotId).ToList();

        var shots = await dbContext.Shots
            .Where(s => s.AlbumId == dto.SourceAlbumId && shotsList.Contains(s.ShotId))
            .ToListAsync();

        foreach (Shot shot in shots) {
            shot.AlbumId = dto.TargetAlbumId;
        }

        if (shotsList.Contains(sourceAlbum.PreviewId)) {
            var newPreviewId = await dbContext.Shots
            .Where(s => s.AlbumId == dto.SourceAlbumId && !shotsList.Contains(s.ShotId))
            .Select(s => s.ShotId).FirstAsync();
            sourceAlbum.PreviewId = newPreviewId;
        }

        await dbContext.SaveChangesAsync();

        return Redirect("edit_album?id=" + dto.SourceAlbumId);

    }

}

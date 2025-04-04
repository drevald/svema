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

namespace Controllers;

public class MainController: BaseController {

    public MainController(ApplicationDbContext dbContext, IConfiguration config) : base(dbContext, config) {
    }

    [Authorize]
    [HttpGet("")]
    public async Task<IActionResult> Albums(AlbumsListDTO dto) {
        var albumsList = new AlbumsListDTO();   
        CultureInfo provider = CultureInfo.InvariantCulture;
        IQueryable<Shot> shotsQuerable = dbContext.Shots.Include(s => s.Album);

        if (dto.DateStart != null || dto.DateEnd != null || dto.LocationId > 0)
        {
            shotsQuerable = shotsQuerable
                .Where(s =>
                    (dto.DateStart == null || s.DateStart >= DateTime.ParseExact(dto.DateStart, "yyyy", provider)) &&
                    (dto.DateEnd == null || s.DateEnd <= DateTime.ParseExact(dto.DateEnd, "yyyy", provider)) &&
                    (dto.LocationId <= 0 || s.LocationId == dto.LocationId)
                );
        }
        albumsList.Albums = await (dto.DateStart != null || dto.DateEnd != null || dto.LocationId > 0
            ? shotsQuerable.Select(s => s.Album).Distinct().ToListAsync()
            : dbContext.Albums.ToListAsync());
        albumsList.Locations = await dbContext.Locations.ToListAsync();
        albumsList.DateStart = dto.DateStart;
        albumsList.DateEnd = dto.DateEnd;
        return View(albumsList);
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> MyAlbums(AlbumsListDTO dto) {
        var albumsList = new AlbumsListDTO();   
        CultureInfo provider = CultureInfo.InvariantCulture;
        IQueryable<Shot> shotsQuerable = dbContext.Shots.Include(s => s.Album);

        if (dto.DateStart != null || dto.DateEnd != null || dto.LocationId > 0)
        {
            shotsQuerable = shotsQuerable
                .Where(s =>
                    (dto.DateStart == null || s.DateStart >= DateTime.ParseExact(dto.DateStart, "yyyy", provider)) &&
                    (dto.DateEnd == null || s.DateEnd <= DateTime.ParseExact(dto.DateEnd, "yyyy", provider)) &&
                    (dto.LocationId <= 0 || s.LocationId == dto.LocationId)
                );
        }
        albumsList.Albums = await (dto.DateStart != null || dto.DateEnd != null || dto.LocationId > 0
            ? shotsQuerable.Select(s => s.Album).Distinct().ToListAsync()
            : dbContext.Albums.ToListAsync());
        albumsList.Locations = await dbContext.Locations.ToListAsync();
        albumsList.DateStart = dto.DateStart;
        albumsList.DateEnd = dto.DateEnd;
        return View(albumsList);
    }    


///////////////////   ALBUM  /////////////////////////////////////////

    [HttpGet("edit_album")]
    public async Task<IActionResult> EditAlbum(int id) {
        AlbumDTO dto = new AlbumDTO();
        var album = await dbContext.Albums.Include(a => a.AlbumComments).Where(a => a.AlbumId==id).FirstAsync();
        var shots = await dbContext.Shots.Where(s => s.Album.AlbumId == id).OrderBy(s => s.ShotId).ToListAsync();
        dto.AlbumId = album.AlbumId;
        dto.Name = album.Name;
        dto.AlbumComments = album.AlbumComments;
        dto.Locations = await dbContext.Locations.ToListAsync();
        dto.Shots = new List<ShotPreviewDTO>();
        foreach (var shot in shots) {
            dto.Shots.Add(
                new ShotPreviewDTO(shot)
            );
        }

        return View(dto);

    }

    [HttpPost("edit_album")]
    public async Task<IActionResult> StoreAlbum(AlbumDTO dto) {
        Console.Write("!!!!STORE ALBUM");
        Console.WriteLine("!!!!STORE ALBUM");
        Album storedAlbum = await dbContext.Albums.FindAsync(dto.AlbumId);
        storedAlbum.Name = dto.Name;
        foreach (var s in dto.Shots)  {
            if (s.IsChecked) {
                Shot shot = await dbContext.Shots.FindAsync(s.ShotId);
                if (dto.Year < 0) {
                    shot.DateEnd = DateTime.MinValue;
                    shot.DateStart = DateTime.MinValue;
                }
                if (DateTime.MinValue != dto.DateStart) {
                    shot.DateStart = dto.DateStart;
                }
                if (DateTime.MinValue != dto.DateEnd) {
                    shot.DateEnd = dto.DateEnd;
                }
                if (dto.LocationId > 0) {
                    shot.LocationId = dto.LocationId;
                } else if (dto.LocationId < 0) {
                    shot.LocationId = null;
                }
                await dbContext.SaveChangesAsync();
            }
        }
        System.Console.Write("STORING ALBUM (" + dto.AlbumId + ")");
        await dbContext.SaveChangesAsync();
        return Redirect("/");
    }

    [HttpGet("add_album")]
    public IActionResult AddAlbum() {
        return View();
    }

    [HttpPost("add_album")]
    public async Task<IActionResult> CreateAlbum(Album album) {
        Console.Write("STORING ALBUM (" + album.AlbumId + ")");
        User user = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).First();
        album.User = user;
        dbContext.Add(album);
        await dbContext.SaveChangesAsync();
        return Redirect("/");
    }

    //todo - cascade delete originals
    [HttpGet("delete_album")]
    public async Task<IActionResult> DeleteAlbum(int id) {
        Album album = await dbContext.Albums.FindAsync(id);
        List<Shot> shots = dbContext.Shots.Where(s => s.AlbumId == id).Include(s => s.Storage).ToList();
        foreach (Shot s in shots) {
            Storage.DeleteFile(s);
        }
        dbContext.Remove(album);
        await dbContext.SaveChangesAsync();
        return Redirect("/");
    }

    [HttpGet("view_album")]
    public async Task<IActionResult> ViewAlbum(int id) {
        var album = await dbContext.Albums.Include(a => a.AlbumComments).Where(a => a.AlbumId==id).FirstAsync();
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
    public async Task<IActionResult> EditShot(int id) {
        var shot = await dbContext.Shots.FindAsync(id);
        var album = await dbContext.Albums.FindAsync(shot.AlbumId);        
        var location = await dbContext.Locations.FindAsync(shot.LocationId);
        ShotDTO dto = new ShotDTO(shot);
        var locations = await dbContext.Locations.ToListAsync();
        dto.Locations = locations;    
        dto.Location = location;
        dto.IsCover = shot.ShotId == album.PreviewId;    
        return View(dto);
    }

    [HttpPost("edit_shot")]
    public async Task<IActionResult> StoreShot(ShotDTO dto) {
        Shot shot = await dbContext.Shots.FindAsync(dto.ShotId);
        Album album = await dbContext.Albums.FindAsync(shot.AlbumId);
        shot.LocationId = dto.LocationId;
        shot.Name = dto.Name;
        shot.DateStart = dto.DateStart;
        shot.DateEnd = dto.DateEnd;
        if (dto.IsCover) {
            album.PreviewId = dto.ShotId;
        }
        await dbContext.SaveChangesAsync();
        return Redirect("edit_album?id=" + shot.AlbumId);
    }

    [HttpGet("shots")]
    public async Task<IActionResult> GetShots() {
        var result = await dbContext.Shots.ToListAsync();
        return View();
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(int id) {
        Console.WriteLine("PREVIEW " + id);
        var result = await dbContext.Shots.FindAsync(id);
        Console.WriteLine("PREVIEW result is " + result);
        var stream = new MemoryStream();
        stream.Write(result.Preview, 0, result.Preview.Length);
        stream.Position = 0;
        return new FileStreamResult(stream, "image/jpeg");
    }

    [HttpGet("shot")]
    public IActionResult Shot(int id) {
        var shot = dbContext.Shots.Where(s => s.ShotId==id).Include(s => s.Storage).First();
        var stream = Storage.GetFile(shot);
        stream.Position = 0;
        return new FileStreamResult(stream, shot.ContentType);
    }

    [HttpGet("upload_shots")]
    public IActionResult UploadFile(int id) {
        var dto = new UploadedFilesDTO();
        dto.AlbumId = id;
        return View(dto);
    }

    [RequestSizeLimit(1000_000_000)]
    [HttpPost("upload_shots")]
    public async Task<IActionResult> StoreFile(UploadedFilesDTO dto) {
        User user = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).First();
        ShotStorage storage = dbContext.ShotStorages.Where(s => s.User == user).FirstOrDefault(s => true);
        if (storage == null) {
            dto.ErrorMessage = "No file storage available";
            return View(dto);
        }
        await dbContext.SaveChangesAsync();
        Album album = await dbContext.Albums.FindAsync(dto.AlbumId);
        long size = dto.Files.Sum(f => f.Length);
        var filePaths = new List<string>();
        dto.FileErrors = new Dictionary<string, string>();
        foreach (var formFile in dto.Files) {            
            if (formFile.Length > 0) {
                using var fileStream = formFile.OpenReadStream();
                byte[] bytes = new byte[formFile.Length];
                fileStream.Read(bytes, 0, (int)formFile.Length);
                var shot = new Shot();
                try {
                    await ProcessShot(bytes, formFile.FileName, formFile.ContentType, shot, album, storage, dto.FileErrors);
                } catch (Exception e) {
                    Console.Write(e.Message);
                }
                
            }
        }
        return View(dto);
    }

/////////////////////       LOCATIONS        //////////////////////////////////////////////////////////

    [HttpGet("delete_location")]
    public async Task<IActionResult> DeleteLocation(int locationId) {
        Location location = await dbContext.Locations.FindAsync(locationId);
        dbContext.Remove(location);
        await dbContext.SaveChangesAsync();
        return Redirect("locations");
    }

    [HttpGet("add_location")]
    public async Task<IActionResult> AddLocation(int locationId) {
        Location location = new Location();
        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync();
        //return EditLocation(location.Id);
        return Redirect("edit_location?LocationId=" + location.Id);
    }

    [HttpGet("edit_location")]
    public async Task<IActionResult> EditLocation(int locationId) {
        Location location = await dbContext.Locations.FindAsync(locationId);
        return View(location);
    }

    [HttpPost("edit_location")]
    public async Task<IActionResult> SaveLocation(Location location) {        
        dbContext.Update(location);
        await dbContext.SaveChangesAsync();
        return Redirect("locations");
    }

    [HttpGet("view_shot")]
    public async Task<IActionResult> ViewShot(int id) {
        var shot = await dbContext.Shots
            .Include(s => s.Location)
            .Include(s => s.ShotComments)
            .Include(s => s.Album)
            .FirstOrDefaultAsync(s => s.ShotId == id);
        return View(shot);
    }

    [HttpGet("delete_shot")]
    public async Task<IActionResult> DeleteShot(int id) {
        Shot shot = dbContext.Shots.Where(s => s.ShotId == id).Include(s => s.Storage).First();
        var albumId = shot.AlbumId;
        Storage.DeleteFile(shot);
        dbContext.Remove(shot);
        await dbContext.SaveChangesAsync();
        return Redirect("/edit_album?id=" + albumId);
    }

    [HttpGet("view_next_shot")]
    public async Task<IActionResult> ViewNextShot(int id) {
        var shot = await dbContext.Shots.FindAsync(id);
        var shots = dbContext.Shots.Where(s => s.AlbumId == shot.AlbumId).ToList<Shot>();
        int index = shots.FindIndex(a => a.ShotId == id);
        try {
            return Redirect("/view_shot?id=" + shots[index + 1].ShotId) ;
        } catch (Exception) {
            return Redirect("/view_shot?id=" + id) ;
        }
    }

    [HttpGet("view_prev_shot")]
    public async Task<IActionResult> ViewPrevShot(int id) {
        var shot = await dbContext.Shots.FindAsync(id);
        var shots = dbContext.Shots.Where(s => s.AlbumId == shot.AlbumId).ToList<Shot>();
        int index = shots.FindIndex(a => a.ShotId == id);
        try {
            return Redirect("/view_shot?id=" + shots[index - 1].ShotId) ;
        } catch (Exception) {
            return Redirect("/view_shot?id=" + id) ;
        }
    }

    [HttpPost("add_comment")]
    public async Task<IActionResult> AddComment(string text, int id, int commentId) {
        var comment = new AlbumComment();    
        User user = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).First();
        if (commentId==0) {
            comment.Author = user;
            comment.AuthorId = user.UserId;
            comment.AuthorUsername = user.Username;
            comment.Text = text;
            comment.AlbumId = id;
            comment.Timestamp = DateTime.Now;
            dbContext.AlbumComments.Add(comment);    
        } else {            
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
    public async Task<IActionResult> DeleteComment(int commentId, int id) {
        var comment = await dbContext.AlbumComments.FindAsync(commentId);
        dbContext.AlbumComments.Remove(comment);
        await dbContext.SaveChangesAsync();
        return Redirect("view_album?id=" + id);
    }

    [HttpPost("add_shot_comment")]
    public async Task<IActionResult> AddShotComment(string text, int id, int commentId) {
        var comment = new ShotComment();    
        User user = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).First();
        if (commentId==0) {
            comment.Author = user;
            comment.AuthorId = user.UserId;
            comment.AuthorUsername = user.Username;            
            comment.Text = text;
            comment.ShotId = id;
            comment.Timestamp = DateTime.Now;
            dbContext.ShotComments.Add(comment);    
        } else {            
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
    public async Task<IActionResult> DeleteShotComment(int commentId, int id) {
        var comment = await dbContext.ShotComments.FindAsync(commentId);
        dbContext.ShotComments.Remove(comment);
        await dbContext.SaveChangesAsync();
        return Redirect("view_shot?id=" + id);
    }

    [HttpGet("locations")]
    public async Task<IActionResult> Locations() {
        var locations = await dbContext.Locations.ToListAsync<Location>();
        return View(locations);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile() {
        var dto = new ProfileDTO();
        dto.User = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).FirstOrDefault(e => true);
        dto.Storages = await dbContext.ShotStorages.Where(s => s.User == dto.User).ToListAsync<ShotStorage>();
        return View(dto);
    }

    [HttpGet("edit_local_storage")]
    public IActionResult EditLocalStorage(int userId, int storageId) {
        var dto = new StorageDTO();
        if (storageId != 0) {
            dto.Storage = dbContext.ShotStorages.Where(s => s.Id == storageId).First();
        } else {
            dto.Storage = new ShotStorage();
            dto.Storage.Root = "/storage";
            dto.Storage.Provider = Provider.Local;
            dto.Storage.UserId = userId;
        }
        return View(dto);
    }

    [HttpPost("edit_local_storage")]
    public async Task<IActionResult> SaveLocalStorage(StorageDTO dto) {
        dbContext.AddOrUpdateEntity(dto.Storage);
        await dbContext.SaveChangesAsync();
        return Redirect("profile?user_id=" + dto.Storage.UserId);
    }

}

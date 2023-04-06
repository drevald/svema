using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using svema.Data;

namespace svema.Controllers;

public class MainController: Controller {

    ApplicationDbContext dbContext;

    IConfiguration config;

    public MainController (ApplicationDbContext dbContext, IConfiguration config) {
        this.dbContext = dbContext;
        this.config = config;
    }

    [Authorize]
    [HttpGet("")]
    public async Task<IActionResult> Index() {
        var albums = await dbContext.Albums.OrderBy(a => a.DateFrom).ToListAsync(); 
        return View(albums);
    }

    [HttpGet("edit_album")]
    public async Task<IActionResult> EditAlbum(int id) {

        var album = await dbContext.Albums.Include(a => a.AlbumComments).Where(a => a.AlbumId==id).FirstAsync();
        var shots = await dbContext.Shots.Where(s => s.Album.AlbumId == id).ToListAsync();
        var locations = await dbContext.AlbumLocations.Where(a => a.Album.AlbumId == id).Include(al => al.Location).ToListAsync();
        ViewBag.locations = locations;        
        ViewBag.album = album;
        ViewBag.shots = shots;
        ViewBag.albumId = id;

        return View(album);

        // System.Console.Write("EDIT ALBUM");
        // Album album = await dbContext.Albums.FindAsync(id);
        // return View(album);
    }

    [HttpGet("add_album")]
    public IActionResult AddAlbum() {
        return View();
    }

    [HttpPost("add_album")]
    public async Task<IActionResult> CreateAlbum(Album album) {
        System.Console.Write("STORING ALBUM (" + album.AlbumId + ")");
        dbContext.Add(album);
        await dbContext.SaveChangesAsync();
        return Redirect("/");
    }

    [HttpPost("edit_album")]
    public async Task<IActionResult> StoreAlbum(Album album) {
        System.Console.Write("STORING ALBUM (" + album.AlbumId + ")");
        dbContext.Update(album);
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
        var shots = await dbContext.Shots.Where(s => s.Album.AlbumId == id).ToListAsync();
        var locations = await dbContext.AlbumLocations.Where(a => a.Album.AlbumId == id).Include(al => al.Location).ToListAsync();
        ViewBag.locations = locations;        
        ViewBag.album = album;
        ViewBag.shots = shots;
        ViewBag.albumId = id;
        return View(album);
    }

    [HttpGet("shots")]
    public async Task<IActionResult> GetShots() {
        var result = await dbContext.Shots.ToListAsync();
        return View();
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(int id) {
        var result = await dbContext.Shots.FindAsync(id);
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
        ViewBag.albumId = id;
        return View();
    }

    [HttpGet("set_as_thumbnail")]
    public async Task<IActionResult> SetAsThumbnail(int id, int shotId) {
        Console.Write("Setting preview id = " + shotId + " for album " +  id);
        var album = dbContext.Albums.Find(id);
        album.PreviewId = shotId;
        dbContext.Update(album);
        await dbContext.SaveChangesAsync();
        return Redirect("/view_album?id=" + id);
    }

    [RequestSizeLimit(1000_000_000)]
    [HttpPost("upload_shots")]
    public async Task<IActionResult> StoreFile(List<IFormFile> files, int albumId) {
        User user = dbContext.Users.Where(u => u.Username == HttpContext.User.Identity.Name).Include(u => u.Storage).First();
        // ShotStorage storage = await dbContext.ShotStorages.FindAsync(1);
        // user.Storage = storage;
        // dbContext.Update(user);
        await dbContext.SaveChangesAsync();
        Album album = await dbContext.Albums.FindAsync(albumId);
        long size = files.Sum(f => f.Length);
        var filePaths = new List<string>();
        using var md5 = MD5.Create();
        var errors = new Dictionary<string, string>();
        foreach (var formFile in files) {            
            if (formFile.Length > 0) {
                try {
                    using var stream = new MemoryStream();
                    using var stream1 = new MemoryStream();
                    using var outputStream = new MemoryStream();
                    await formFile.CopyToAsync(stream);
                    await formFile.CopyToAsync(stream1);
                    stream.Position = 0;
                    stream1.Position = 0;
                    using var image = Image.Load(stream);
                    float ratio = (float)image.Width/(float)image.Height;
                    if (ratio > 1 ) {
                        image.Mutate(x => x.Resize((int)(200 * ratio), 200));
                        image.Mutate(x => x.Crop(new Rectangle((image.Width-200)/2, 0, 200, 200)));
                    } else {
                        image.Mutate(x => x.Resize(200, (int)(200 / ratio)));
                        image.Mutate(x => x.Crop(new Rectangle(0, (image.Height-200)/2, 200, 200)));
                    }
                    ImageExtensions.SaveAsJpeg(image, outputStream);
                    Shot shot = new Shot();
                    shot.ContentType = formFile.ContentType;
                    shot.Name = formFile.FileName;
                    shot.Album = album;
                    shot.Preview = outputStream.GetBuffer();
                    stream.Position = 0;

                    shot.MD5 = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    dbContext.Shots.Add(shot);
                    await dbContext.SaveChangesAsync();
                    if (album.PreviewId == 0) {
                        album.PreviewId = shot.ShotId;
                        dbContext.Albums.Update(album);
                    }
                    shot.SourceUri = "" + shot.ShotId;
                    shot.Storage = user.Storage;
                    Storage.StoreShot(shot, stream1.GetBuffer());
                    await dbContext.SaveChangesAsync();
                }   catch (DbUpdateException e) {
                    System.Console.Write("The DbUpdateException is " + e.Data);
                    errors.Add(formFile.FileName, e.InnerException.Message);
                }   catch (Exception e) {
                    System.Console.Write("The Exception is " + e.Data);
                    errors.Add(formFile.FileName, e.Message);
                } 
            }

        }
        ViewBag.albumId = albumId;        
        ViewBag.errors = errors;
        return View();
    }

    [HttpGet("delete_location")]
    public async Task<IActionResult> DeleteLocation(int locationId, int albumId) {
        Location location = await dbContext.Locations.FindAsync(locationId);
        dbContext.Remove(location);
        await dbContext.SaveChangesAsync();
        return Redirect("/add_location?albumId=" + albumId);
    }


    // [HttpGet("add_location")]
    // public async Task<IActionResult> AddLocation(int albumId) {
    //     var locations = await dbContext.Locations.ToListAsync();
    //     var album = await dbContext.Albums.FindAsync(albumId);
    //     ViewBag.albumId = albumId;
    //     ViewBag.locations = locations;
    //     var albumLocation = new AlbumLocation();
    //     return View(new Location());
    // }

    // [HttpPost("add_location")]
    // public async Task<IActionResult> StoreLocation(Location location, int albumId) {
    //     dbContext.Locations.Add(location);
    //     await dbContext.SaveChangesAsync();
    //     return Redirect("/add_location?albumId=" + albumId);
    // }

    [HttpGet("select_location")]
    public async Task<IActionResult> SelectLocation(int locationId, int albumId) {
        var albumLocation = new AlbumLocation();
        albumLocation.AlbumId = albumId;
        albumLocation.LocationId = locationId;  
        dbContext.AlbumLocations.Add(albumLocation);
        await dbContext.SaveChangesAsync();
        return Redirect("/view_album?id=" + albumId);
    }
    
    [HttpGet("album_location_view")]
    public async Task<IActionResult> ViewAlbumLocation(int albumLocationId) {
        var albumLocation = await dbContext.AlbumLocations.FindAsync(albumLocationId);
        var locations = await dbContext.Locations.ToListAsync();
        var location = await dbContext.Locations.FindAsync(albumLocation.LocationId);
        ViewBag.locations = locations;
        ViewBag.albumId = albumLocation.AlbumId;
        return View("AddLocation", location);
    }

    [HttpGet("album_location_delete")]
    public async Task<IActionResult> DeleteAlbumLocation(int albumLocationId) {
        var albumLocation = await dbContext.AlbumLocations.FindAsync(albumLocationId);
        var albumId = albumLocation.AlbumId;
        dbContext.Remove(albumLocation);
        await dbContext.SaveChangesAsync();
        return Redirect("/view_album?id=" + albumId);
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
        return Redirect("/view_album?id=" + albumId);
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

    [HttpGet("shot_location_set")]
    public async Task<IActionResult> SetShotLocation(int shotId) {
        var locations = await dbContext.Locations.ToListAsync<Location>();        
        var shot = await dbContext.Shots.FindAsync(shotId);
        var location = shot.Location == null ? new Location() : shot.Location;
        ViewBag.shotId = shotId;
        ViewBag.locations = locations;
        return View("ShotLocation", location);
    }

    [HttpPost("shot_location_set")]
    public async Task<IActionResult> SaveShotLocation(Location location, int shotId) {
        var shot = await dbContext.Shots.FindAsync(shotId);
        if (location.Id == 0) {
            dbContext.Add(location);
        } else {
            dbContext.Update(location);
        }
        shot.Location = location;
        dbContext.Update(shot);
        await dbContext.SaveChangesAsync();
        return Redirect("view_shot?id=" + shot.ShotId);
    }

    [HttpGet("shot_location_delete")]
    public async Task<IActionResult> DeleteShotLocation(int shotId) {
        var shot = await dbContext.Shots.Include(s => s.Location).FirstOrDefaultAsync(s => s.ShotId == shotId);
        if (shot.Location != null && shot.Location.Name == null) {
            dbContext.Remove(shot.Location);
        }
        shot.Location = null;
        await dbContext.SaveChangesAsync();
        return Redirect("view_shot?id=" + shot.ShotId);
    }

    [HttpGet("set_album_date")]
    public IActionResult SetAlbumDate(int albumId) {
        return View();
    }

    [HttpPost("set_album_date")]
    public async Task<IActionResult> SetShotDate(string year_from, string month_from, string day_from, string year_to, string month_to, string day_to, string date_format, int id) {
        var album = await dbContext.Albums.FindAsync(id);
        DateTime date_from = new DateTime(Int32.Parse(year_from), Int32.Parse(month_from), Int32.Parse(day_from));
        DateTime date_to = new DateTime(Int32.Parse(year_to), Int32.Parse(month_to), Int32.Parse(day_to));
        album.DatePrecision = date_format;
        album.DateFrom = date_from;
        album.DateTo = date_to;
        dbContext.Update(album);
        await dbContext.SaveChangesAsync();
        return Redirect("view_album?id=" + id);
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
            comment.Timestamp = new DateTime();
            dbContext.AlbumComments.Add(comment);    
        } else {            
            comment = await dbContext.AlbumComments.FindAsync(commentId);
            comment.Text = text;
            comment.AlbumId = id;
            comment.Timestamp = new DateTime();            
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
            comment.Timestamp = new DateTime();
            dbContext.ShotComments.Add(comment);    
        } else {            
            comment = await dbContext.ShotComments.FindAsync(commentId);
            comment.Text = text;
            comment.ShotId = id;
            comment.Timestamp = new DateTime();            
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

    [HttpGet("add_location")]
    public IActionResult AddLocation() {
        var location = new Location();
        return View(location);   
    }

}

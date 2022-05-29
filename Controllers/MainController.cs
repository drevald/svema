using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using svema.Data;

namespace svema.Controllers;

public class MainController: Controller {

    ApplicationDbContext dbContext;

    public MainController (ApplicationDbContext dbContext) {
        this.dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index() {
        var albums = await dbContext.Albums.ToListAsync(); 
        return View(albums);
    }

    [HttpGet("upload_album")]
    public IActionResult UploadAlbum() {
        return View();
    }

    [HttpPost("upload_album")]
    public IActionResult StoreAlbum(List<IFormFile> files) {
        System.Console.Write("STORE ALBUM\n");
        return Ok("");
    }

    [HttpGet("edit_album")]
    public async Task<IActionResult> EditAlbum(int id) {
        System.Console.Write("EDIT ALBUM");
        Album album = await dbContext.Albums.FindAsync(id);
        return View(album);
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

    [HttpGet("delete_album")]
    public async Task<IActionResult> DeleteAlbum(int id) {
        Album album = await dbContext.Albums.FindAsync(id);
        dbContext.Albums.Remove(album);
        await dbContext.SaveChangesAsync();
        return Redirect("/");
    }

    [HttpGet("view_album")]
    public async Task<IActionResult> ViewAlbum(int id) {
        IEnumerable<Shot> shots = await dbContext.Shots.Where(s => s.Album.AlbumId == id).ToListAsync();
        ViewBag.shots = shots;
        ViewBag.albumId = id;
        return View();
    }


    // [HttpGet("films")]
    // public async Task<IActionResult> GetFilms() {
    //     var result = await dbContext.Albums.ToListAsync();
    //     return Redirect("/");
    // }

    // [HttpPost("films")]
    // public Task<IActionResult> PostFilm(Album album) {
    //     return null;
    // }

    [HttpGet("shots")]
    public async Task<IActionResult> GetShots() {
        var result = await dbContext.Shots.ToListAsync();
        return View();
    }

    [HttpGet("shot")]
    public async Task<IActionResult> GetShot() {
        var result = await dbContext.Shots.ToListAsync();
        return View();
    }

    [HttpGet("upload_shots")]
    public IActionResult UploadFile(int id) {
        ViewBag.albumId = id;
        return View();
    }

    [HttpPost("upload_shots")]
    public async Task<IActionResult> StoreFile(List<IFormFile> files, int albumId) {
        System.Console.Write("ALBUM ID IS " + albumId);
        long size = files.Sum(f => f.Length);
        var filePaths = new List<string>();
        foreach (var formFile in files) {
            System.Console.Write("\n=============\n");
            System.Console.Write(formFile.Name);
            System.Console.Write("\n");
            System.Console.Write(formFile.FileName);
            System.Console.Write("\n");
            System.Console.Write(formFile.ContentDisposition);
            System.Console.Write("\n=============\n");
            if (formFile.Length > 0) {
                // full path to file in temp location
                var filePath = Path.GetTempFileName(); //we are using Temp file name just for the example. Add your own file path.
                filePaths.Add(filePath);
                using (var stream = new FileStream(filePath + ".TMP", FileMode.Create)) {
                    System.Console.Write(filePath);
                    await formFile.CopyToAsync(stream);
                }
            }
        }

        return Ok(new { count = files.Count, size, filePaths });
    }


}
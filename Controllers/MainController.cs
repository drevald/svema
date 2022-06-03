using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

using svema.Data;

namespace svema.Controllers;

public class MainController: Controller {

    ApplicationDbContext dbContext;

    IConfiguration config;

    public MainController (ApplicationDbContext dbContext, IConfiguration config) {
        this.dbContext = dbContext;
        this.config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index() {
        var albums = await dbContext.Albums.ToListAsync(); 
        return View(albums);
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
        dbContext.Remove(album);
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
    public async Task<IActionResult> Shot(int id) {
        var shot = await dbContext.Shots.FindAsync(id);
        var stream = System.IO.File.OpenRead(shot.SourceUri);
        stream.Position = 0;
        return new FileStreamResult(stream, shot.ContentType);
    }

    [HttpGet("upload_shots")]
    public IActionResult UploadFile(int id) {
        ViewBag.albumId = id;
        return View();
    }

    [RequestSizeLimit(1000_000_000)]
    [HttpPost("upload_shots")]
    public async Task<IActionResult> StoreFile(List<IFormFile> files, int albumId) {
        Album album = await dbContext.Albums.FindAsync(albumId);
        long size = files.Sum(f => f.Length);
        var filePaths = new List<string>();
        using var md5 = MD5.Create();

        foreach (var formFile in files) {            
            if (formFile.Length > 0) {

                using var stream = new MemoryStream();
                using var outputStream = new MemoryStream();
                await formFile.CopyToAsync(stream);
                stream.Position = 0;
                using var image = Image.Load(stream);
                image.Mutate(x => x.Resize(100, 100));
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

                stream.Position = 0;
                System.IO.File.WriteAllBytes(config["STORAGE_DIR"] + shot.ShotId, stream.GetBuffer());                
                shot.SourceUri = config["STORAGE_DIR"] + shot.ShotId;
                await dbContext.SaveChangesAsync();

            }
        }
        return Redirect("/view_album?id=" + albumId);
    }

}
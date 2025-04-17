using System;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using Data;
using Form;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Text;


namespace Controllers;

[Route("api")]
public class RestController: BaseController {

    public RestController(ApplicationDbContext dbContext, IConfiguration config) : base(dbContext, config) {
    }

    [HttpGet("albums")]
    public JsonResult GetAlbums() {
        var albums = dbContext.Albums;
        return new JsonResult(albums);
    }

    [HttpGet("albums/{albumId}")]
    public JsonResult GetAlbum(int albumId) {
        var album = dbContext.Albums.Find(albumId);
        return new JsonResult(album);
    }

    [HttpPost("shots")]
    public async Task PostShot([FromBody] ShotREST dto) {

        // Request.EnableBuffering(); // <-- This is important if you're also using [FromBody]
        // using var reader = new StreamReader(Request.Body, encoding: Encoding.UTF8, leaveOpen: true);
        // var rawBody = await reader.ReadToEndAsync();
        // Request.Body.Position = 0; // Reset stream position for model binding to still work

        // Console.WriteLine("RAW JSON: " + rawBody);

        var album = dbContext.Albums.Find(dto.AlbumId);
        var user = dbContext.Users.Where(u => u.UserId==dto.UserId).First();
        var storage = dbContext.ShotStorages.Where(s => s.User==user).First();
        var errors = new Dictionary<string, string>();
        var shot = new Shot();
        shot.DateStart = dto.DateStart;
        shot.DateEnd = dto.DateEnd;
        shot.Album = album;
        shot.AlbumId = dto.AlbumId;
        shot.OrigPath = dto.OrigPath;
        shot.DateUploaded = DateTime.Now;
        await ProcessShot(dto.Data, dto.Name, dto.Mime, shot, album, storage, errors);
    }

    [HttpPost("albums")]
    public JsonResult PostAlbum([FromBody] AlbumDTO dto) {
        
        var user = dbContext.Users.Where(u => u.UserId == dto.UserId).FirstOrDefault();
        
        // Check if user exists
        if (user == null) {
            return new JsonResult(new { message = "User not found" }) { StatusCode = 404 };
        }

        // Check if the album already exists for the given user
        var existingAlbum = dbContext.Albums
                                    .Where(a => a.User.UserId == dto.UserId && a.Name == dto.Name)
                                    .FirstOrDefault();
        
        if (existingAlbum != null) {
            // If album exists, return the existing one
            return new JsonResult(existingAlbum);
        }

        // If album doesn't exist, create a new one
        Album album = new Album {
            User = user,
            Name = dto.Name
        };
        dbContext.Add(album);
        dbContext.SaveChanges();

        return new JsonResult(album);
    }


    [HttpGet("albums/{albumId}/shots")]
    public JsonResult GetShots(int albumId) {
        var shots = dbContext.Shots.Where(s => s.AlbumId == albumId);
        return new JsonResult(shots);
    }

    [HttpGet("shots/{id}")]
    public JsonResult GetShot(int id) {
        var shott = dbContext.Shots.Find(id);
        return new JsonResult(shott);
    }

}
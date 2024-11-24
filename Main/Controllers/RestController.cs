using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Data;
using Form;

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
        var album = dbContext.Albums.Find(dto.AlbumId);
        var user = dbContext.Users.Where(u => u.UserId==dto.UserId).First();
        var storage = dbContext.ShotStorages.Where(s => s.User==user).First();
        var errors = new Dictionary<string, string>();
        var shot = new Shot();
        shot.DateStart = dto.DateStart;
        shot.DateEnd = dto.DateEnd;
        shot.Album = album;
        shot.AlbumId = dto.AlbumId;
        await ProcessShot(dto.Data, dto.Name, dto.Mime, shot, album, storage, errors);
    }

    [HttpPost("albums")]
    public JsonResult PostAlbum([FromBody] AlbumDTO dto) {
        var user = dbContext.Users.Where(u => u.UserId==dto.UserId).First();
        Album album = new Album();
        album.User = user;
        album.Name = dto.Name;
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
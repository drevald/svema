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
    public async Task<JsonResult> PostShot([FromBody] ShotREST dto) {
        var album = dbContext.Albums.Find(dto.AlbumId);
        var user = dbContext.Users.Where(u => u.UserId==dto.UserId).Include(u => u.Storage).First();
        var errors = new Dictionary<string, string>();
        await ProcessShot(dto.Data, dto.Name, dto.Mime, album, user.Storage, errors);
    //public async Task<Dictionary<string, string>> ProcessShot(byte[] data, string name, string mime, Album album, ShotStorage storage, Dictionary<string, string> errors) {
        Shot shot = new Shot();
        dbContext.Add(shot);
        dbContext.SaveChanges();
        return new JsonResult(shot);
    }

    [HttpPost("albums")]
    public JsonResult PostAlbum([FromBody] AlbumDTO dto) {
        Album album = new Album();
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
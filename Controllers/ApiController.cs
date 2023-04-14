using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using svema.Data;
using svema.Form;
using System.Text.Json;

namespace svema.Controllers;

[Route("api")]
public class RestController: Controller {

    ApplicationDbContext dbContext;

    IConfiguration config;

    public RestController (ApplicationDbContext dbContext, IConfiguration config) {
        this.dbContext = dbContext;
        this.config = config;
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
    public JsonResult PostShot(Shot shot) {
        dbContext.Add(shot);
        dbContext.SaveChanges();
        return new JsonResult(shot);
    }

    [HttpPost("albums")]
    public JsonResult PostAlbum([FromBody] AlbumCUT dto) {
        Album album = new Album();
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
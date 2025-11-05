using System;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using Data;
using Form;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Controllers;

[Route("api")]
public class RestController : BaseController {

    public RestController(ApplicationDbContext dbContext, IConfiguration config) : base(dbContext, config) {
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("users/{userId}/shots/{shotId}/comments")]
    public async Task AddComment(int userId, int shotId, [FromBody] AddCommentDto dto) {
        commentService.AddShotCommentForApi(userId, shotId, dto.Caption);
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("shots/{userId}/uncommented")]
    public IActionResult GetUncommentedShots(int userId, int offset = 0, int size = 17) {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var shots = shotService.GetUncommentedShots(userId, username, offset, size);
        return new JsonResult(shots);
    }

    [HttpGet("albums")]
    public JsonResult GetAlbums()
    {
        var albums = albumService.GetAlbums();
        return new JsonResult(albums);
    }

    [HttpGet("albums/{albumId}")]
    public JsonResult GetAlbum(int albumId)
    {
        var album = albumService.GetAlbum(albumId);
        return new JsonResult(album);
    }

    [HttpPost("shots")]
    public async Task<IActionResult> PostShot([FromBody] ShotREST dto)
    {
        var album = albumService.GetAlbum(dto.AlbumId);
        var user = userService.GetUserById(dto.UserId);
        if (user == null)
        {
            return BadRequest(new { error = "User not found." });
        }

        var storage = userService.GetStorageForUser(user.UserId);
        if (storage == null)
        {
            return BadRequest(new { error = "Storage not found for user." });
        }

        var errors = new Dictionary<string, string>();
        var shot = new Shot
        {
            DateStart = dto.DateStart,
            DateEnd = dto.DateEnd,
            Album = album,
            AlbumId = dto.AlbumId,
            OrigPath = dto.OrigPath,
            DateUploaded = DateTime.Now
        };

        await fileService.ProcessShot(dto.Data, dto.Name, dto.Mime, shot, album, storage, errors);

        if (errors.Any())
        {
            return BadRequest(errors);
        }

        return Ok(new { message = "Shot uploaded successfully" });
    }


    [HttpPost("albums")]
    public JsonResult PostAlbum([FromBody] AlbumDTO dto)
    {
        var user = userService.GetUserById(dto.UserId);

        if (user == null)
        {
            return new JsonResult(new { message = "User not found" }) { StatusCode = 404 };
        }

        var album = albumService.FindOrCreateAlbum(dto.UserId, dto.Name);

        return new JsonResult(album);
    }


    [HttpGet("albums/{albumId}/shots")]
    public JsonResult GetShots(int albumId)
    {
        var shots = shotService.GetShotsByAlbum(albumId);
        return new JsonResult(shots);
    }

    [HttpGet("shots/{id}")]
    public JsonResult GetShot(int id)
    {
        var shot = shotService.GetShot(id);
        return new JsonResult(shot);
    }

}
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
        var username = User.Identity?.Name;
        var comment = new ShotComment
        {
            Text = dto.Caption,
            ShotId = shotId,
            AuthorId = userId,
            Timestamp = DateTime.Now,
            AuthorUsername = username
        };

        dbContext.ShotComments.Add(comment);
        await dbContext.SaveChangesAsync();
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("shots/{userId}/uncommented")]
    public IActionResult GetUncommentedShots(int userId, int offset = 0, int size = 20) {
        // Get current authenticated user's username
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var shots = dbContext.Shots
            .Where(shot =>
                // Shots owned by the user
                shot.Album.User.UserId == userId && shot.Album.User.Username == username ||

                // Shared via SharedUsers
                dbContext.SharedUsers.Any(su =>
                    su.GuestUser.Username == username &&
                    su.HostUser.UserId == shot.Album.User.UserId) ||

                // Shared via SharedAlbums
                dbContext.SharedAlbums.Any(sa =>
                    sa.GuestUser.Username == username &&
                    sa.Album.AlbumId == shot.Album.AlbumId)
            )
            // Exclude shots already commented on by this user
            .Where(shot =>
                !shot.ShotComments.Any(c => c.Author.Username == username)
            )
            .OrderByDescending(s => s.ShotId) // Optional: for consistent pagination
            .Skip(offset)
            .Take(size)
            .ToList();

        return new JsonResult(shots);
    }

    [HttpGet("albums")]
    public JsonResult GetAlbums()
    {
        var albums = dbContext.Albums;
        return new JsonResult(albums);
    }

    [HttpGet("albums/{albumId}")]
    public JsonResult GetAlbum(int albumId)
    {
        var album = dbContext.Albums.Find(albumId);
        return new JsonResult(album);
    }

    [HttpPost("shots")]
    public async Task<IActionResult> PostShot([FromBody] ShotREST dto)
    {
        var album = dbContext.Albums.Find(dto.AlbumId);
        var user = dbContext.Users.FirstOrDefault(u => u.UserId == dto.UserId);
        if (user == null)
        {
            return BadRequest(new { error = "User not found." });
        }

        var storage = dbContext.ShotStorages.FirstOrDefault(s => s.User == user);
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

        await ProcessShot(dto.Data, dto.Name, dto.Mime, shot, album, storage, errors);

        if (errors.Any())
        {
            return BadRequest(errors); // returns dictionary as JSON
        }

        return Ok(new { message = "Shot uploaded successfully" });
    }


    [HttpPost("albums")]
    public JsonResult PostAlbum([FromBody] AlbumDTO dto)
    {

        var user = dbContext.Users.Where(u => u.UserId == dto.UserId).FirstOrDefault();

        // Check if user exists
        if (user == null)
        {
            return new JsonResult(new { message = "User not found" }) { StatusCode = 404 };
        }

        // Check if the album already exists for the given user
        var existingAlbum = dbContext.Albums
                                    .Where(a => a.User.UserId == dto.UserId && a.Name == dto.Name)
                                    .FirstOrDefault();

        if (existingAlbum != null)
        {
            // If album exists, return the existing one
            return new JsonResult(existingAlbum);
        }

        // If album doesn't exist, create a new one
        Album album = new Album
        {
            User = user,
            Name = dto.Name
        };
        dbContext.Add(album);
        dbContext.SaveChanges();

        return new JsonResult(album);
    }


    [HttpGet("albums/{albumId}/shots")]
    public JsonResult GetShots(int albumId)
    {
        var shots = dbContext.Shots.Where(s => s.AlbumId == albumId);
        return new JsonResult(shots);
    }

    [HttpGet("shots/{id}")]
    public JsonResult GetShot(int id)
    {
        var shott = dbContext.Shots.Find(id);
        return new JsonResult(shott);
    }

}
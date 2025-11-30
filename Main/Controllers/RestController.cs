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
using Utils;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Svema.Controllers;

[Route("api")]
public class RestController(ApplicationDbContext dbContext, IConfiguration config) : BaseController(dbContext, config)
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("users/{userId}/shots/{shotId}/comments")]
    public void AddComment(int userId, int shotId, [FromBody] AddCommentDto dto)
    {
        commentService.AddShotCommentForApi(userId, shotId, dto.Caption);
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("shots/{userId}/uncommented")]
    public IActionResult GetUncommentedShots(int userId, int offset = 0, int size = 17)
    {
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

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDTO dto)
    {
        try
        {
            Console.WriteLine($"Login attempt for user: {dto.Username}");
            var user = dbContext.Users.FirstOrDefault(u => u.Username == dto.Username && u.PasswordHash == dto.Password);
            if (user == null)
            {
                Console.WriteLine("User not found or password incorrect");
                return Unauthorized(new { message = "User or password are incorrect" });
            }

            // Create JWT token
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, "Member")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Blessed is he who, in the name of charity and good will shepherds the weak through the valley of darkness"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "yourdomain.com",
                audience: "yourdomain.com",
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new { token = tokenString, userId = user.UserId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex}");
            return StatusCode(500, new { message = ex.Message, stack = ex.StackTrace });
        }
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("shots")]
    public async Task<IActionResult> PostShot([FromBody] ShotREST dto)
    {
        // Get authenticated user from JWT token
        var authenticatedUserId = GetUserId();
        if (!authenticatedUserId.HasValue)
        {
            return Unauthorized(new { error = "Authentication required." });
        }

        var user = userService.GetUserById(authenticatedUserId.Value);
        if (user == null)
        {
            return Unauthorized(new { error = "User not found." });
        }

        var album = albumService.GetAlbum(dto.AlbumId);
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

        PhotoMetadata photoMetadata = fileService.GetMetadata(dto.Data, dto.Name, errors);
        await fileService.ProcessShot(dto.Data, dto.Name, dto.Mime, shot, album, storage, errors, photoMetadata);

        if (errors.Count > 0)
        {
            return BadRequest(errors);
        }

        return Ok(new { message = "Uploaded", lat = shot.Latitude, lon = shot.Longitude, date = shot.DateStart, album = album.Name });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("albums")]
    public JsonResult PostAlbum([FromBody] AlbumDTO dto)
    {
        // Get authenticated user from JWT token
        var authenticatedUserId = GetUserId();
        if (!authenticatedUserId.HasValue)
        {
            return new JsonResult(new { message = "Authentication required." }) { StatusCode = 401 };
        }

        var user = userService.GetUserById(authenticatedUserId.Value);
        if (user == null)
        {
            return new JsonResult(new { message = "User not found." }) { StatusCode = 404 };
        }

        var album = albumService.FindOrCreateAlbum(user.UserId, dto.Name);

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
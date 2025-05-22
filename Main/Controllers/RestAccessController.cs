using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Text; // For Encoding
using Microsoft.IdentityModel.Tokens; // For SymmetricSecurityKey, SigningCredentials, SecurityAlgorithms
using System.IdentityModel.Tokens.Jwt;
using Data;
using Form;

namespace Controllers;

[Route("api")]
public class RestAccessController : Controller
{

    ApplicationDbContext dbContext;

    IConfiguration config;

    public RestAccessController(ApplicationDbContext dbContext, IConfiguration config)
    {
        this.dbContext = dbContext;
        this.config = config;
    }


    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        var user = dbContext.Users.FirstOrDefault(u => u.Username == dto.Username && u.PasswordHash == dto.Password);
        if (user == null)
        {
            return Unauthorized(new { message = "User or password are incorrect" });
        }

        // Create JWT token (example)
        var claims = new List<Claim>
        {
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

        return Ok(new { token = tokenString });
    }

}
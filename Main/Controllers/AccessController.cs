using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Data;
using Form;

namespace Controllers;

public class AccessController : Controller
{
    private readonly ApplicationDbContext dbContext;
    private readonly IConfiguration config;

    public AccessController(ApplicationDbContext dbContext, IConfiguration config)
    {
        this.dbContext = dbContext;
        this.config = config;
    }

    [HttpGet("register")]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost("register")]
    public async Task<IActionResult> DoRegister(string username, string password, string email)
    {
        try
        {
            // ❗️TODO: Use a proper password hasher (not plain text or MD5)
            var user = new User
            {
                Username = username,
                PasswordHash = password, // Replace with a hash in the future
                Email = email
            };

            dbContext.Add(user);
            await dbContext.SaveChangesAsync();

            return Redirect("/login");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return Redirect("/register");
        }
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        return View(new LoginDTO());
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDTO dto)
    {
        try
        {
            // Try to find the user
            var user = dbContext.Users
                .FirstOrDefault(u => u.Username == dto.Username && u.PasswordHash == dto.Password);

            if (user == null)
            {
                dto.ErrorMessage = "User or password are incorrect";
                return View(dto);
            }

            // ✅ Standard, framework-compatible claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),   // user ID
                new Claim(ClaimTypes.Name, user.Username),                  // username
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),    // optional email
                new Claim(ClaimTypes.Role, "Member")                        // default role
            };

            // ✅ Build principal and sign in
            var identity = new ClaimsIdentity(claims, "CookieScheme");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("CookieScheme", principal); // specify scheme explicitly

            return Redirect("/");
        }
        catch (Exception e)
        {
            dto.ErrorMessage = "Failed to login: " + e.Message;
            Console.WriteLine(e.Message);
            return View(dto);
        }
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("CookieScheme");
        return Redirect("/");
    }
}
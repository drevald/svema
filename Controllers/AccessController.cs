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
using Data;

namespace Controllers;

public class AccessController: Controller {

    ApplicationDbContext dbContext;

    IConfiguration config;

    public AccessController (ApplicationDbContext dbContext, IConfiguration config) {
        this.dbContext = dbContext;
        this.config = config;
    }

    [HttpGet("register")]
    public IActionResult Register() {
        return View();
    }

    [HttpPost("register")]
    public async Task<IActionResult> DoRegister(string username, string password, string email) {
        var md5 = MD5.Create();
        var user = new User();
        user.Username = username;
        user.PasswordHash = password;
        user.Email = email;
        dbContext.Add(user);
        await dbContext.SaveChangesAsync();
        return Redirect("/login");
    } 

    [HttpGet("login")]
    public IActionResult Login() {
        return View();
    }

    [HttpPost("login")]
    public async Task<IActionResult> DoLogin(string username, string password) {
        User user = dbContext.Users.Where(u => u.Username == username).Where(u => u.PasswordHash == password).First();
        if (user != null) {
            var claims = new List<Claim> {
                new Claim("user", username),
                new Claim("role", "Member")
            };
            await HttpContext.SignInAsync(
                new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "Cookies", "user", "role")));
            return Redirect("/");
        } else {
            return Redirect("/error");
        }
    } 

    [HttpGet("logout")]
    public async Task<IActionResult> Logout() {
        await HttpContext.SignOutAsync();
        return Redirect("/");
    }

}
using System;
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
using Form;

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
        try {
            var md5 = MD5.Create();
            var user = new User();
            user.Username = username;
            user.PasswordHash = password;
            user.Email = email;
            dbContext.Add(user);
            await dbContext.SaveChangesAsync();
            return Redirect("/login");
        } catch (Exception e) {
            Console.Write(e.Message);
            return Redirect("/register");
        }
    } 

    [HttpGet("login")]
    public IActionResult Login() {
        LoginDTO dto = new LoginDTO();
        return View(dto);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDTO dto) {
        try {
            User user = dbContext.Users.Where(u => u.Username == dto.Username).Where(u => u.PasswordHash == dto.Password).First();
            if (user != null) {
                var claims = new List<Claim> {
                    new Claim("user", dto.Username),
                    new Claim("role", "Member")
                };
                await HttpContext.SignInAsync(
                    new ClaimsPrincipal(
                        new ClaimsIdentity(claims, "Cookies", "user", "role")));
                return Redirect("/");
            } else {
                dto.ErrorMessage = "User or password are incorrect";
                //return Redirect("/login");
                return View(dto);
            }
        } catch (Exception e) {
            dto.ErrorMessage = "Failed to login: " + e.Message;
            Console.WriteLine(e.Message);
            // return Redirect("/login");
            return View(dto);
        }

    } 

    [HttpGet("logout")]
    public async Task<IActionResult> Logout() {
        await HttpContext.SignOutAsync();
        return Redirect("/");
    }

}
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using svema.Data;

namespace svema.Controllers;

public class MainController: Controller {

    ApplicationDbContext dbContext;

    public MainController (ApplicationDbContext dbContext) {
        this.dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index() {
        // var blog = new Blog () {

        // };
        // dbContext.Add(blog);            
        // dbContext.SaveChanges();

        var result = await dbContext.Shots.ToListAsync(); 
        // System.Console.WriteLine(">>>>>>>");
        // System.Console.WriteLine(result);
        // System.Console.WriteLine("<<<<<<<");
        return Ok("hi there");
    }

    [HttpGet("films")]
    public async Task<IActionResult> GetFilms() {
        // System.Console.Write("GETTING\n");
        var result = await dbContext.Films.ToListAsync();
        // System.Console.Write("GOT\n");
        //return View();
        return Ok("");
    }

    [HttpPost("films")]
    public Task<IActionResult> PostFilm(Film film) {
        // System.Console.Write("POSTING\n");
        // dbContext.Add(blog);            
        // dbContext.SaveChanges();
        // System.Console.Write("POSTED\n");
        return null;
    }

    [HttpGet("shots")]
    public async Task<IActionResult> GetShots() {
        var result = await dbContext.Shots.ToListAsync();
        return View();
    }

    [HttpGet("shot")]
    public async Task<IActionResult> GetShot() {
        var result = await dbContext.Shots.ToListAsync();
        return View();
    }

}
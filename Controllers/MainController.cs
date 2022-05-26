using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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

    [HttpGet("upload")]
    public IActionResult UploadFile() {
        return View();
    }

    [HttpPost("upload")]
    public async Task<IActionResult> StoreFile(List<IFormFile> files) {

        long size = files.Sum(f => f.Length);

        var filePaths = new List<string>();
        foreach (var formFile in files) {
            System.Console.Write("\n=============\n");
            System.Console.Write(formFile.Name);
            System.Console.Write("\n");
            System.Console.Write(formFile.FileName);
            System.Console.Write("\n");
            System.Console.Write(formFile.ContentDisposition);
            System.Console.Write("\n=============\n");
            if (formFile.Length > 0) {
                // full path to file in temp location
                var filePath = Path.GetTempFileName(); //we are using Temp file name just for the example. Add your own file path.
                filePaths.Add(filePath);
                using (var stream = new FileStream(filePath + ".TMP", FileMode.Create)) {
                    System.Console.Write(filePath);
                    await formFile.CopyToAsync(stream);
                }
            }
        }

        // process uploaded files
        // Don't rely on or trust the FileName property without validation.
        return Ok(new { count = files.Count, size, filePaths });
    }


}
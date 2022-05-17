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
        var blog = new Blog () {

        };
        dbContext.Add(blog);            
        dbContext.SaveChanges();

        var result = await dbContext.Blogs.ToListAsync(); 
        System.Console.WriteLine(">>>>>>>");
        System.Console.WriteLine(result);
        System.Console.WriteLine("<<<<<<<");
        return Ok("hi there");
    }

}
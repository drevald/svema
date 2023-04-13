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
using svema.Data;

namespace svema.Controllers;

public class RestController: Controller {

    ApplicationDbContext dbContext;

    IConfiguration config;

    public RestController (ApplicationDbContext dbContext, IConfiguration config) {
        this.dbContext = dbContext;
        this.config = config;
    }

    [HttpGet("api/{id}")]
    public JsonResult Get(int id)
    {
        var album = dbContext.Albums.Find(id);
        return new JsonResult(album);
    }


}
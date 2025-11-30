using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Data;
using Form;

namespace Svema.Controllers;

public class SharedLinkController(ApplicationDbContext dbContext, IConfiguration config) : BaseController(dbContext, config)
{
    [HttpGet("share_link")]
    public IActionResult ShareLink(int id, string type)
    {
        SharedLink sharedLink = new(
            type,
            id,
            GetUserId() ?? 0,
            null
        );

        dbContext.Add(sharedLink);
        dbContext.SaveChanges();

        var request = HttpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}/view_{type}{request.QueryString}";

        SharedLinkDTO dto = new()
        {
            SharedLink = sharedLink,
            BaseUrl = baseUrl,
            LinkWithToken = baseUrl + "&token=" + sharedLink.Token
        };

        return View(dto);
    }
}
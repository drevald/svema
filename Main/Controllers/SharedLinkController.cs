using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Data;
using Form;

namespace Controllers;

public class SharedLinkController : BaseController
{
    public SharedLinkController(ApplicationDbContext dbContext, IConfiguration config) : base(dbContext, config)
    {
    }

    [HttpGet("share_link")]
    public IActionResult ShareLink(int id, string type)
    {
        SharedLink sharedLink = new SharedLink(
            type,
            id,
            GetUserId() ?? 0,
            null
        );

        dbContext.Add(sharedLink);
        dbContext.SaveChanges();

        var request = HttpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}/view_{type}{request.QueryString}";

        SharedLinkDTO dto = new SharedLinkDTO();
        dto.SharedLink = sharedLink;
        dto.BaseUrl = baseUrl;
        dto.LinkWithToken = baseUrl + "&token=" + sharedLink.Token;

        return View(dto);
    }

}
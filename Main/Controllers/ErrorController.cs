using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class ErrorController : Controller
{
    [Route("Error/{code:int}")]
    public IActionResult HandleStatusCode(int code)
    {
        ViewData["Code"] = code;
        return View("Error", new { StatusCode = code });
    }

    [Route("Error")]
    public IActionResult HandleException()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        ViewData["Path"] = feature?.Path;
        ViewData["ErrorMessage"] = feature?.Error?.Message;
        return View("Error");
    }
}

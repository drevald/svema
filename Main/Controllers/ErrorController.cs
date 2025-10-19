using Microsoft.AspNetCore.Mvc;

public class ErrorController : Controller
{
    [Route("Error/{statusCode}")]
    public IActionResult HttpStatusCodeHandler(int statusCode)
    {
        switch (statusCode)
        {
            case 404:
                return View("NotFound"); // Views/Error/NotFound.cshtml
            case 403:
                return View("Forbidden"); // Views/Error/Forbidden.cshtml
            default:
                return View("Error"); // generic fallback
        }
    }

    [Route("Error")]
    public IActionResult Error()
    {
        return View("Error");
    }
}

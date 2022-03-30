using Microsoft.AspNetCore.Mvc;

namespace svema.Controllers;

public class MainController: Controller {

    [HttpGet("")]
    public IActionResult Index() {
        return View();
    }

}
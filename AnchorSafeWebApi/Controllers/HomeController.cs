using log4net;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace AnchorSafe.API.Controllers;

[Route("")]
public class HomeController : Controller
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    [HttpGet]
    public IActionResult Index()
    {
        log.Info("Entering HomeController.Index()");
        ViewData["Title"] = "API | Anchor Safe";
        log.Debug($"HomeController.Index | ViewData.Title set to '{ViewData["Title"]}'");
        log.Info("Exiting HomeController.Index()");
        return Content("AnchorSafe API");
    }
}

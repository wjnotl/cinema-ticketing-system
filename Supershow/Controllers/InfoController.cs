using Microsoft.AspNetCore.Mvc;

namespace Supershow.Controllers;

public class InfoController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("About");
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }
}


using Microsoft.AspNetCore.Mvc;

namespace Supershow.Controllers;

public class ErrorController : Controller
{
    [Route("Error/{status_code}")]
    public IActionResult Index(int status_code)
    {
        ViewBag.StatusCode = status_code;

        switch (status_code)
        {
            case 401:
                ViewBag.Message = "Unauthorized";
                ViewBag.Description = "You are not authorized to access this page.";
                break;
            case 403:
                ViewBag.Message = "Access Denied";
                ViewBag.Description = "You are not allowed to access this page.";
                break;
            case 404:
                ViewBag.Message = "Page Not Found";
                ViewBag.Description = "It looks like something is missing!";
                break;
            case 405:
                ViewBag.Message = "Method Not Allowed";
                ViewBag.Description = "The method you are trying to access is not allowed.";
                break;
            case 500:
                ViewBag.Message = "Internal Server Error";
                ViewBag.Description = "An unexpected error occurred on the server.";
                break;
            default:
                ViewBag.Message = "Error";
                ViewBag.Description = "An error occurred.";
                break;
        }

        return View();
    }
}
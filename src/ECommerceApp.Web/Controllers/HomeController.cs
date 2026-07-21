using System.Diagnostics;
using ECommerceApp.Application.Storefront;
using Microsoft.AspNetCore.Mvc;
using ECommerceApp.Web.Models;

namespace ECommerceApp.Web.Controllers;

public class HomeController : Controller
{
    private readonly IHomePageService _homePageService;

    public HomeController(IHomePageService homePageService)
    {
        _homePageService = homePageService;
    }

    public async Task<IActionResult> Index()
    {
        var homePage = await _homePageService.GetHomePageAsync(HttpContext.RequestAborted);
        return View(homePage);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [Route("/Home/StatusCode/{code:int}")]
    public IActionResult HandleStatusCode(int code)
    {
        if (code == StatusCodes.Status404NotFound)
        {
            return View("NotFound");
        }

        if (code == StatusCodes.Status403Forbidden)
        {
            return RedirectToAction(nameof(AccessDenied));
        }

        return View("Error", new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = code,
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

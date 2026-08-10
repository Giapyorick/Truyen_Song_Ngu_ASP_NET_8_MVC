using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;
using WebTruyenTranh.Helpers;

namespace WebTruyenTranh.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly TruyenSongNguContext _context;
    private readonly IWebHostEnvironment _env;

    public HomeController(ILogger<HomeController> logger, TruyenSongNguContext context, IWebHostEnvironment env)
    {
        _logger = logger;
        _context = context;
        _env = env;
    }

    public IActionResult Index()
    {
        ViewBag.DebugUserId = HttpContext.Session.GetInt32("UserId");
        ViewBag.DebugUserName = HttpContext.Session.GetString("UserName");
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

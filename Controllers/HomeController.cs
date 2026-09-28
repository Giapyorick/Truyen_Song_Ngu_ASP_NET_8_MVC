using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;
using WebTruyenTranh.ViewModels;

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
    [HttpGet]
    public async Task<IActionResult> FilterStories(string? keyword)
    {
        int currentUserId = HttpContext.Session.GetInt32("UserId") ?? 0;

        var query = _context.TblStories
            .AsNoTracking()
            .Include(s => s.Author)
            .Include(s => s.TblChapters)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string term = keyword.Trim().ToLower();
            query = query.Where(s => (s.Title != null && s.Title.ToLower().Contains(term))
                                  || (s.Author != null && s.Author.AuthorName != null && s.Author.AuthorName.ToLower().Contains(term)));
        }

        var stories = await query
            .OrderByDescending(s => s.StoryId)
            .Take(12)
            .Select(s => new StoryListViewModel
            {
                StoryID = s.StoryId,
                Title = s.Title ?? "Untitled",
                Img = s.Img ?? "assets/image/placeholder.png",
                Rate = s.Rate,
                Likes = s.Likes,
                CountFolower = s.CountFolower,
                Lang = s.Lang,
                HasProgress = false,
                LatestChapters = s.TblChapters
                .OrderByDescending(c => c.ChapterNumber)
                .Take(3)
                .Select(c => new LatestChapterItemViewModel 
                {
                    ChapterId = c.ChapterId,
                    ChapterNumber = c.ChapterNumber,
                    ChapterTitle = c.Title
                })
                .ToList()
            })
            .ToListAsync();

        // Trả về HTML của partial view đã có sẵn
        return PartialView("_StoryCardsPartial", stories);
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

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
    [Route("Home/FilterStories")]
    [Route("FilterStories")] // Hỗ trợ cả /FilterStories lẫn /Home/FilterStories
    public async Task<IActionResult> FilterStories(string? keyword)
    {
        int currentUserId = HttpContext.Session.GetInt32("UserId") ?? 0;

        var rqCulture = HttpContext.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>();
        var cultureName = rqCulture?.RequestCulture.UICulture.Name
                          ?? Request.Cookies[Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName]
                          ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
        bool isVi = cultureName.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

        var query = _context.TblStories
            .AsNoTracking()
            .Include(s => s.Author)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string term = keyword.Trim().Normalize(System.Text.NormalizationForm.FormC);
            string searchPattern = $"%{term}%";

            if (isVi)
            {
                query = query.Where(s =>
                    s.TblStoryTranslations.Any(t => t.LanguageCode.StartsWith("vi") &&
                        (EF.Functions.Like(t.Title, searchPattern) || t.Title.Contains(term)))
                    || (!s.TblStoryTranslations.Any(t => t.LanguageCode.StartsWith("vi")) &&
                        (EF.Functions.Like(s.Title, searchPattern) || s.Title.Contains(term)))
                    || (s.Author != null && s.Author.AuthorName != null && EF.Functions.Like(s.Author.AuthorName, searchPattern))
                );
            }
            else
            {
                query = query.Where(s =>
                    (s.Title != null && (EF.Functions.Like(s.Title, searchPattern) || s.Title.Contains(term)))
                    || (s.Author != null && s.Author.AuthorName != null && EF.Functions.Like(s.Author.AuthorName, searchPattern))
                );
            }
        }

        var stories = await query
            .OrderByDescending(s => s.StoryId)
            .Take(12)
            .Select(s => new StoryListViewModel
            {
                StoryID = s.StoryId,
                Title = isVi
                    ? (s.TblStoryTranslations
                        .Where(t => t.LanguageCode.StartsWith("vi"))
                        .Select(t => t.Title)
                        .FirstOrDefault() ?? s.Title ?? "Chưa đặt tên")
                    : (s.Title ?? "Untitled"),
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
                        ChapterTitle = isVi
                            ? (c.TblChapterTranslations
                                .Where(ct => ct.LanguageCode.StartsWith("vi"))
                                .Select(ct => ct.Title)
                                .FirstOrDefault() ?? c.Title)
                            : c.Title
                    })
                    .ToList()
            })
            .ToListAsync();

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

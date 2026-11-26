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
    private readonly IEmailSenderService _emailSender;
    private readonly IConfiguration _config;

    public HomeController(ILogger<HomeController> logger, TruyenSongNguContext context, IWebHostEnvironment env,
                          IEmailSenderService emailSender, IConfiguration config)
    {
        _logger = logger;
        _context = context;
        _env = env;
        _emailSender = emailSender;
        _config = config;
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
                        .FirstOrDefault() ?? s.Title ?? "No name")
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

        // Gắn tiến độ đọc (huy hiệu "Chương X") nếu người dùng đã đăng nhập
        await ReadingProgressHelper.FillAsync(_context, stories, currentUserId);

        return PartialView("_StoryCardsPartial", stories);
    }

    // ======================================================================
    // TRANG THÔNG TIN: Giới thiệu / Liên hệ / Chính sách quyền riêng tư
    // (cần cho việc xét duyệt Google AdSense)
    // ======================================================================

    /// <summary>Email nhận liên hệ: SiteInfo:ContactEmail, nếu trống thì dùng MailSettings:Mail</summary>
    private string ContactEmail =>
        !string.IsNullOrWhiteSpace(_config["SiteInfo:ContactEmail"])
            ? _config["SiteInfo:ContactEmail"]!
            : (_config["MailSettings:Mail"] ?? "");

    private void SetSiteInfo()
    {
        ViewBag.SiteName = _config["SiteInfo:Name"] ?? "BilingualManga";
        ViewBag.ContactEmail = ContactEmail;
    }

    [Route("about")]
    [Route("Home/About")]
    public IActionResult About()
    {
        SetSiteInfo();
        return View();
    }

    [HttpGet]
    [Route("contact")]
    [Route("Home/Contact")]
    public IActionResult Contact()
    {
        SetSiteInfo();
        return View(new ContactFormViewModel());
    }

    [HttpPost]
    [Route("contact")]
    [Route("Home/Contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactFormViewModel model)
    {
        SetSiteInfo();
        bool isVi = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

        // Bot điền vào ô bẫy -> giả vờ thành công, không gửi
        if (!string.IsNullOrEmpty(model.Website))
        {
            TempData["ContactSent"] = true;
            return RedirectToAction(nameof(Contact));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Chống gửi liên tục: tối đa 1 tin / 60 giây cho mỗi phiên
        long lastSent = long.TryParse(HttpContext.Session.GetString("ContactLastSent"), out var t) ? t : 0;
        if (DateTime.UtcNow.Ticks - lastSent < TimeSpan.FromSeconds(60).Ticks)
        {
            ModelState.AddModelError(string.Empty, isVi
                ? "Bạn vừa gửi tin nhắn. Vui lòng đợi một phút rồi thử lại."
                : "You just sent a message. Please wait a minute and try again.");
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(ContactEmail))
        {
            ModelState.AddModelError(string.Empty, isVi
                ? "Hộp thư liên hệ chưa được cấu hình."
                : "The contact inbox is not configured yet.");
            return View(model);
        }

        string Enc(string? v) => System.Net.WebUtility.HtmlEncode(v ?? "");
        string body = $@"
            <h3>Tin nhắn mới từ trang Liên hệ</h3>
            <p><b>Họ tên:</b> {Enc(model.Name)}<br>
               <b>Email:</b> {Enc(model.Email)}<br>
               <b>Chủ đề:</b> {Enc(model.Topic)}<br>
               <b>Thời gian (UTC):</b> {DateTime.UtcNow:yyyy-MM-dd HH:mm}</p>
            <p style=""white-space:pre-wrap"">{Enc(model.Message)}</p>";

        try
        {
            await _emailSender.SendEmailAsync(ContactEmail, $"[Liên hệ] {model.Topic} - {model.Name}", body);
            HttpContext.Session.SetString("ContactLastSent", DateTime.UtcNow.Ticks.ToString());
            TempData["ContactSent"] = true;
            return RedirectToAction(nameof(Contact));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email liên hệ thất bại");
            ModelState.AddModelError(string.Empty, isVi
                ? "Không gửi được tin nhắn lúc này. Bạn có thể email trực tiếp cho chúng tôi."
                : "We couldn't send your message right now. You can email us directly.");
            return View(model);
        }
    }

    [Route("privacy")]
    [Route("Home/Privacy")]
    public IActionResult Privacy()
    {
        SetSiteInfo();
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Controllers
{
    public class RankingsController : Controller
    {
        private readonly TruyenSongNguContext _context;

        public RankingsController(TruyenSongNguContext context)
        {
            _context = context;
        }

        // GET: /Rankings?type=likes
        [HttpGet]
        public IActionResult Index(string type = "likes")
        {
            ViewBag.CurrentType = (type ?? "likes").ToLower();

            // KHÔNG cần query 20 truyện ở đây, để InfiniteScroller tự gọi API GetStoriesInfinite phân trang
            return View(new List<StoryListViewModel>());
        }
        // GET: /Rankings/GetRankedStoriesPartial?type=likes
        [HttpGet]
        public async Task<IActionResult> GetRankedStoriesPartial(string type = "likes")
        {
            var stories = await QueryRankedStories(type);
            return PartialView("_StoryCardsPartial", stories);
        }

        private async Task<List<StoryListViewModel>> QueryRankedStories(string type)
        {
            // 1. Nhận diện chuẩn ngôn ngữ hiện tại của người dùng
            var rqCulture = HttpContext.Features.Get<IRequestCultureFeature>();
            var cultureName = rqCulture?.RequestCulture.UICulture.Name
                              ?? Request.Cookies[CookieRequestCultureProvider.DefaultCookieName]
                              ?? CultureInfo.CurrentUICulture.Name;

            bool isVi = cultureName.StartsWith("vi", System.StringComparison.OrdinalIgnoreCase);
            string langPrefix = isVi ? "vi" : "en";

            var query = _context.TblStories.AsNoTracking();

            // 2. Sắp xếp theo từng loại tiêu chí
            query = (type?.ToLower()) switch
            {
                "rate" => query.OrderByDescending(s => s.Rate)
                               .ThenByDescending(s => s.CountRate)
                               .ThenByDescending(s => s.StoryId),
                "follower" => query.OrderByDescending(s => s.CountFolower)
                                   .ThenByDescending(s => s.Likes)
                                   .ThenByDescending(s => s.StoryId),
                "most_rated" => query.OrderByDescending(s => s.CountRate)
                                     .ThenByDescending(s => s.Rate)
                                     .ThenByDescending(s => s.StoryId),
                _ => query.OrderByDescending(s => s.Likes)
                          .ThenByDescending(s => s.CountFolower)
                          .ThenByDescending(s => s.StoryId)
            };

            // 3. Ánh xạ song ngữ chính xác
            return await query
                .Take(20)
                .Select(s => new StoryListViewModel
                {
                    StoryID = s.StoryId,
                    // Tiếng Việt lấy từ TblStoryTranslations, Tiếng Anh lấy từ s.Title gốc
                    Title = isVi
                        ? (s.TblStoryTranslations
                            .Where(t => t.LanguageCode.StartsWith("vi"))
                            .Select(t => t.Title)
                            .FirstOrDefault() ?? s.Title ?? "Undefined")
                        : (s.Title ?? "Undefined"),
                    Img = s.Img,
                    Lang = s.Lang,
                    Rate = s.Rate,
                    Likes = s.Likes,
                    CountFolower = s.CountFolower,
                    CountRate = s.CountRate,
                    HasProgress = false,
                    LatestChapters = _context.TblChapters
                        .Where(ch => ch.StoryId == s.StoryId)
                        .OrderByDescending(ch => ch.ChapterNumber)
                        .Take(3)
                        .Select(ch => new LatestChapterItemViewModel
                        {
                            ChapterId = ch.ChapterId,
                            ChapterNumber = ch.ChapterNumber,
                            ChapterTitle = isVi
                                ? (ch.TblChapterTranslations
                                    .Where(ct => ct.LanguageCode.StartsWith("vi"))
                                    .Select(ct => ct.Title)
                                    .FirstOrDefault() ?? ch.Title)
                                : ch.Title
                        }).ToList()
                })
                .ToListAsync();
        }
    }
}
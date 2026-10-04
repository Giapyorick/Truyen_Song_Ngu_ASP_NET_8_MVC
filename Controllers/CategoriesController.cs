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
    public class CategoriesController : Controller
    {
        private readonly TruyenSongNguContext _context;

        public CategoriesController(TruyenSongNguContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId)
        {
            var rqCulture = HttpContext.Features.Get<IRequestCultureFeature>();
            var cultureName = rqCulture?.RequestCulture.UICulture.Name
                              ?? Request.Cookies[CookieRequestCultureProvider.DefaultCookieName]
                              ?? CultureInfo.CurrentUICulture.Name;

            bool isVi = cultureName.StartsWith("vi", System.StringComparison.OrdinalIgnoreCase);

            var categories = await _context.TblCategories
                .AsNoTracking()
                .Where(c => !string.IsNullOrEmpty(c.Name))
                .Select(c => new CategoryItemDto
                {
                    CategoryId = c.CategoryId,
                    Name = isVi
                        ? (c.TblCategoryTranslations
                            .Where(ct => ct.LanguageCode.StartsWith("vi"))
                            .Select(ct => ct.Name)
                            .FirstOrDefault() ?? c.Name)
                        : c.Name,
                    Description = isVi
                        ? (c.TblCategoryTranslations
                            .Where(ct => ct.LanguageCode.StartsWith("vi"))
                            .Select(ct => ct.Description)
                            .FirstOrDefault() ?? c.Description)
                        : c.Description,
                    StoryCount = _context.TblCategoryOfStories.Count(cs => cs.CategoryId == c.CategoryId)
                })
                .ToListAsync();

            if (!categoryId.HasValue && categories.Any())
            {
                categoryId = categories.First().CategoryId;
            }

            ViewBag.Categories = categories;
            ViewBag.SelectedCategoryId = categoryId;

            var selectedCat = categories.FirstOrDefault(c => c.CategoryId == categoryId);
            ViewBag.SelectedCategoryName = selectedCat?.Name ?? "";
            ViewBag.SelectedCategoryDesc = selectedCat?.Description ?? "";

            // KHÔNG cần query danh sách truyện ở đây nữa, truyền List rỗng để InfiniteScroller tự tải Page 1
            return View(new List<StoryListViewModel>());
        }

        [HttpGet]
        public async Task<IActionResult> GetStoriesPartial(int categoryId)
        {
            var rqCulture = HttpContext.Features.Get<IRequestCultureFeature>();
            var cultureName = rqCulture?.RequestCulture.UICulture.Name
                              ?? Request.Cookies[CookieRequestCultureProvider.DefaultCookieName]
                              ?? CultureInfo.CurrentUICulture.Name;

            bool isVi = cultureName.StartsWith("vi", System.StringComparison.OrdinalIgnoreCase);
            string langPrefix = isVi ? "vi" : "en";

            var stories = await GetStoriesByCategoryId(categoryId, isVi, langPrefix);
            return PartialView("_StoryCardsPartial", stories);
        }

        private async Task<List<StoryListViewModel>> GetStoriesByCategoryId(int categoryId, bool isVi, string langPrefix)
        {
            return await (from cs in _context.TblCategoryOfStories
                          join s in _context.TblStories on cs.StoryId equals s.StoryId
                          where cs.CategoryId == categoryId
                          select new StoryListViewModel
                          {
                              StoryID = s.StoryId,
                              Title = isVi
                                  ? (s.TblStoryTranslations
                                      .Where(st => st.LanguageCode.StartsWith("vi"))
                                      .Select(st => st.Title)
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
                          .AsNoTracking()
                          .ToListAsync();
        }
    }
}
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Components
{
    /// <summary>
    /// "Truyện liên quan" ở trang chi tiết truyện: các truyện có chung thể loại,
    /// truyện trùng NHIỀU thể loại hơn đứng trước, sau đó tới truyện được thích nhiều.
    /// Hiển thị bằng _StoryCardsPartial (giống trang chủ) kèm tiến độ đọc.
    /// Dùng: @await Component.InvokeAsync("RelatedStories", new { storyId = Model.StoryId })
    /// </summary>
    [ViewComponent(Name = "RelatedStories")]
    public class RelatedStoriesComponent : ViewComponent
    {
        private readonly TruyenSongNguContext _context;

        public RelatedStoriesComponent(TruyenSongNguContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(int storyId, int take = 8)
        {
            bool isVi = CultureInfo.CurrentUICulture.Name.StartsWith("vi", System.StringComparison.OrdinalIgnoreCase);
            string langPrefix = isVi ? "vi" : "en";

            // Thể loại của truyện đang xem
            var categoryIds = await _context.TblCategoryOfStories
                .AsNoTracking()
                .Where(cs => cs.StoryId == storyId)
                .Select(cs => cs.CategoryId)
                .Distinct()
                .ToListAsync();

            var stories = new List<StoryListViewModel>();

            if (categoryIds.Count > 0)
            {
                stories = await _context.TblStories
                    .AsNoTracking()
                    .Where(s => s.StoryId != storyId
                             && s.TblCategoryOfStories.Any(cs => categoryIds.Contains(cs.CategoryId)))
                    .OrderByDescending(s => s.TblCategoryOfStories.Count(cs => categoryIds.Contains(cs.CategoryId)))
                    .ThenByDescending(s => s.Likes)
                    .ThenByDescending(s => s.StoryId)
                    .Take(take)
                    .Select(s => new StoryListViewModel
                    {
                        StoryID = s.StoryId,
                        StoryCode = s.StoryCode,
                        Title = s.TblStoryTranslations
                                    .Where(t => t.LanguageCode.StartsWith(langPrefix))
                                    .Select(t => t.Title)
                                    .FirstOrDefault() ?? s.Title ?? "",
                        Img = s.Img,
                        Lang = s.Lang,
                        Rate = s.Rate,
                        Likes = s.Likes,
                        CountFolower = s.CountFolower,
                        CountRate = s.CountRate,
                        HasProgress = false,
                        LatestChapters = s.TblChapters
                            .OrderByDescending(ch => ch.ChapterNumber)
                            .Take(3)
                            .Select(ch => new LatestChapterItemViewModel
                            {
                                ChapterId = ch.ChapterId,
                                ChapterNumber = ch.ChapterNumber,
                                ChapterTitle = ch.TblChapterTranslations
                                                  .Where(ct => ct.LanguageCode.StartsWith(langPrefix))
                                                  .Select(ct => ct.Title)
                                                  .FirstOrDefault() ?? ch.Title
                            }).ToList()
                    })
                    .ToListAsync();

                int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                await ReadingProgressHelper.FillAsync(_context, stories, userId);
            }

            return View("Default", stories);
        }
    }
}

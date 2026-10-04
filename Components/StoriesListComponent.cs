using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Components
{
    [ViewComponent(Name = "StoriesList")]
    public class StroriesListComponent : ViewComponent
    {
        private readonly TruyenSongNguContext _context;

        public StroriesListComponent(TruyenSongNguContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            // 1. Xác định ngôn ngữ hiện tại
            var currentCulture = CultureInfo.CurrentUICulture.Name;
            bool isVi = currentCulture.StartsWith("vi", System.StringComparison.OrdinalIgnoreCase);
            string langPrefix = isVi ? "vi" : "en";

            // 2. Lấy 8 truyện đầu tiên kèm tiêu đề song ngữ
            var topStories = await _context.TblStories
                .AsNoTracking()
                .OrderByDescending(s => s.StoryId)
                .Take(8)
                .Select(s => new
                {
                    s.StoryId,
                    Title = s.TblStoryTranslations
                             .Where(t => t.LanguageCode.StartsWith(langPrefix))
                             .Select(t => t.Title)
                             .FirstOrDefault() ?? s.Title,
                    s.Img,
                    s.Likes,
                    s.Rate,
                    s.CountFolower,
                    s.CountRate,
                    s.Lang
                })
                .ToListAsync();

            if (!topStories.Any())
            {
                return View("StoriesList", new List<StoryListViewModel>());
            }

            var storyIds = topStories.Select(s => s.StoryId).ToList();

            // 3. Nạp danh sách Chapter kèm bản dịch từ TblChapterTranslations
            var rawChapters = await _context.TblChapters
                .AsNoTracking()
                .Where(c => storyIds.Contains(c.StoryId))
                .Include(c => c.TblChapterTranslations)
                .OrderByDescending(c => c.ChapterNumber)
                .ToListAsync();

            // Nhóm và ánh xạ tiêu đề chương theo đúng ngôn ngữ trên RAM
            var chaptersLookup = rawChapters
                .GroupBy(c => c.StoryId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Take(3).Select(c => {
                        // Tìm bản dịch tiếng Việt hoặc tiếng Anh từ bảng TblChapterTranslation
                        var translatedTitle = c.TblChapterTranslations?
                            .FirstOrDefault(t => t.LanguageCode.StartsWith(langPrefix, System.StringComparison.OrdinalIgnoreCase))?
                            .Title;

                        // Nếu không có bản dịch, mới lấy tiêu đề gốc c.Title
                        var finalTitle = !string.IsNullOrWhiteSpace(translatedTitle) ? translatedTitle : c.Title;

                        return new LatestChapterItemViewModel
                        {
                            ChapterId = c.ChapterId,
                            ChapterNumber = c.ChapterNumber,
                            ChapterTitle = finalTitle
                        };
                    }).ToList()
                );

            // 4. Lấy tiến độ đọc nếu user đã đăng nhập
            var progressDict = new Dictionary<int, (int? LastChapterId, int? ChapterNumber)>();
            if (userId > 0)
            {
                var progressList = await _context.TblUserReadingProgresses
                    .AsNoTracking()
                    .Where(p => p.UserId == userId && storyIds.Contains(p.StoryId))
                    .Select(p => new
                    {
                        p.StoryId,
                        p.LastChapterId
                    })
                    .ToListAsync();

                var chapterNumberDict = rawChapters
                    .GroupBy(c => c.ChapterId)
                    .ToDictionary(g => g.Key, g => g.First().ChapterNumber);

                foreach (var p in progressList)
                {
                    int? chNum = null;
                    if (p.LastChapterId > 0 && chapterNumberDict.TryGetValue(p.LastChapterId, out var foundNum))
                    {
                        chNum = foundNum;
                    }

                    progressDict[p.StoryId] = (p.LastChapterId > 0 ? (int?)p.LastChapterId : null, chNum);
                }
            }

            // 5. Kết hợp ViewModel hoàn chỉnh
            var result = topStories.Select(s =>
            {
                bool hasProgress = progressDict.TryGetValue(s.StoryId, out var prog);

                return new StoryListViewModel
                {
                    StoryID = s.StoryId,
                    Title = s.Title,
                    Img = s.Img,
                    Likes = s.Likes,
                    Rate = s.Rate,
                    CountFolower = s.CountFolower,
                    CountRate = s.CountRate,
                    Lang = s.Lang,
                    HasProgress = hasProgress,
                    LastChapterId = hasProgress ? prog.LastChapterId : null,
                    LastChapterNumber = hasProgress ? prog.ChapterNumber : null,
                    LatestChapters = chaptersLookup.TryGetValue(s.StoryId, out var chList)
                        ? chList
                        : new List<LatestChapterItemViewModel>()
                };
            }).ToList();

            return View("StoriesList", result);
        }
    }
}
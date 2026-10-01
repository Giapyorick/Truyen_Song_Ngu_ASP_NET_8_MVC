using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
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

            // 1. Chỉ lấy đúng 8 truyện đầu tiên với các cột cần thiết (Loại bỏ hoàn toàn subquery lồng nhau)
            var topStories = await _context.TblStories
                .AsNoTracking()
                .OrderByDescending(s => s.StoryId)
                .Take(8)
                .Select(s => new
                {
                    s.StoryId,
                    s.Title,
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

            // 2. Nạp toàn bộ các chương liên quan đến 8 truyện này qua 1 truy vấn duy nhất
            var chapters = await _context.TblChapters
                .AsNoTracking()
                .Where(c => storyIds.Contains(c.StoryId))
                .OrderByDescending(c => c.ChapterNumber)
                .Select(c => new
                {
                    c.StoryId,
                    c.ChapterId,
                    c.ChapterNumber,
                    c.Title
                })
                .ToListAsync();

            // Nhóm chapters theo StoryId và lấy tối đa 3 chương mới nhất trên RAM
            var chaptersLookup = chapters
                .GroupBy(c => c.StoryId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Take(3).Select(c => new LatestChapterItemViewModel
                    {
                        ChapterId = c.ChapterId,
                        ChapterNumber = c.ChapterNumber,
                        ChapterTitle = c.Title
                    }).ToList()
                );

            // 3. Nếu đã đăng nhập, lấy tiến độ đọc tương ứng
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

                // Tạo từ điển tra cứu số chương từ danh sách chapter đã tải ở bước 2
                var chapterNumberDict = chapters
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

            // 4. Ánh xạ sang ViewModel hoàn toàn trong RAM (O(1))
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
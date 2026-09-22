using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            var stories = await _context.TblStories
                .AsNoTracking()
                .OrderByDescending(s => s.StoryId) // Hoặc theo Likes
                .Take(8) // BẮT BUỘC THÊM: Chỉ lấy 8 truyện đầu tiên cho lần tải đầu
                .Select(s => new
                {
                    Story = s,
                    Progress = _context.TblUserReadingProgresses
                        .Where(p => p.UserId == userId && p.StoryId == s.StoryId)
                        .Select(p => new
                        {
                            p.LastChapterId,
                            ChapterNumber = _context.TblChapters
                                .Where(c => c.ChapterId == p.LastChapterId)
                                .Select(c => (int?)c.ChapterNumber)
                                .FirstOrDefault()
                        })
                        .FirstOrDefault(),
                    LatestChapters = _context.TblChapters
                        .Where(c => c.StoryId == s.StoryId)
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
                .Select(x => new StoryListViewModel
                {
                    StoryID = x.Story.StoryId,
                    Title = x.Story.Title,
                    Img = x.Story.Img,
                    Likes = x.Story.Likes,
                    Rate = x.Story.Rate,
                    CountFolower = x.Story.CountFolower,
                    CountRate = x.Story.CountRate,
                    Lang = x.Story.Lang,
                    HasProgress = x.Progress != null,
                    LastChapterId = x.Progress != null ? x.Progress.LastChapterId : null,
                    LastChapterNumber = x.Progress != null ? x.Progress.ChapterNumber : null,
                    LatestChapters = x.LatestChapters
                })
                .ToListAsync();

            return View("StoriesList", stories);
        }
    }
}
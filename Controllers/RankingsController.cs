using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
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
        public async Task<IActionResult> Index(string type = "likes")
        {
            ViewBag.CurrentType = type.ToLower();
            var stories = await QueryRankedStories(type);
            return View(stories);
        }

        // GET: /Rankings/GetRankedStoriesPartial?type=likes (Dùng cho AJAX bấm nút không load lại trang)
        [HttpGet]
        public async Task<IActionResult> GetRankedStoriesPartial(string type = "likes")
        {
            var stories = await QueryRankedStories(type);
            // Tận dụng lại chính _StoryListPartial của bạn
            return PartialView("~/Views/Shared/_StoryListPartial.cshtml", stories);
        }

        private async Task<List<StoryListViewModel>> QueryRankedStories(string type)
        {
            var query = _context.TblStories
                .AsNoTracking();

            // Sắp xếp linh hoạt theo từng loại xếp hạng
            query = type.ToLower() switch
            {
                "rate" => query.OrderByDescending(s => s.Rate).ThenByDescending(s => s.CountRate),
                "follower" => query.OrderByDescending(s => s.CountFolower).ThenByDescending(s => s.Likes),
                "most_rated" => query.OrderByDescending(s => s.CountRate).ThenByDescending(s => s.Rate),
                _ => query.OrderByDescending(s => s.Likes).ThenByDescending(s => s.CountFolower) // Mặc định là 'likes'
            };

            return await query
                .Take(20) // Lấy Top 20 truyện dẫn đầu
                .Select(s => new StoryListViewModel
                {
                    StoryID = s.StoryId,
                    Title = s.Title,
                    Img = s.Img,
                    Lang = s.Lang,
                    Rate = s.Rate,
                    Likes = s.Likes,
                    CountFolower = s.CountFolower,
                    HasProgress = false,
                    LatestChapters = _context.TblChapters
                        .Where(ch => ch.StoryId == s.StoryId)
                        .OrderByDescending(ch => ch.ChapterNumber)
                        .Take(3)
                        .Select(ch => new LatestChapterItemViewModel
                        {
                            ChapterId = ch.ChapterId,
                            ChapterNumber = ch.ChapterNumber,
                            ChapterTitle = ch.Title
                        }).ToList()
                })
                .ToListAsync();
        }
    }
}
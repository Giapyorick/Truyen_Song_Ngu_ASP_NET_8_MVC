using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient; 
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Controllers
{
    public class StoriesController : Controller
    {
        private readonly ILogger<StoriesController> _logger;
        private readonly TruyenSongNguContext _context;

        public StoriesController(ILogger<StoriesController> logger, TruyenSongNguContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index() 
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var stories = await _context.TblStories
                .Select(s => new StoryListViewModel
                {
                    StoryID = s.StoryId,
                    Title = s.Title ?? "Undefined",
                    Img = s.Img,
                    HasProgress = userId > 0 && _context.TblUserReadingProgresses
                        .Any(p => p.UserId == userId && p.StoryId == s.StoryId),

                    LastChapterId = userId > 0 ? _context.TblUserReadingProgresses
                        .Where(p => p.UserId == userId && p.StoryId == s.StoryId)
                        .Select(p => (int?)p.LastChapterId)
                        .FirstOrDefault() : null,

                    Categories = s.TblCategoryOfStories
                        .Select(c => c.Category.Name ?? "Undefined")
                        .ToList()
                })
                .ToListAsync();

            return View(stories);
        }
        [HttpGet]
        public async Task<IActionResult> GetStoriesInfinite(string type = "likes", int? categoryId = null, int page = 1, int pageSize = 8)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var query = _context.TblStories.AsNoTracking();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = from s in query
                        join cs in _context.TblCategoryOfStories on s.StoryId equals cs.StoryId
                        where cs.CategoryId == categoryId.Value
                        select s;
            }

            query = type.ToLower() switch
            {
                "rate" => query.OrderByDescending(s => s.Rate).ThenByDescending(s => s.CountRate),
                "follower" => query.OrderByDescending(s => s.CountFolower).ThenByDescending(s => s.Likes),
                "most_rated" => query.OrderByDescending(s => s.CountRate).ThenByDescending(s => s.Rate),
                _ => query.OrderByDescending(s => s.Likes).ThenByDescending(s => s.CountFolower)
            };

            var stories = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                .Select(x => new StoryListViewModel
                {
                    StoryID = x.Story.StoryId,
                    Title = x.Story.Title,
                    Img = x.Story.Img,
                    Lang = x.Story.Lang,
                    Rate = x.Story.Rate,
                    Likes = x.Story.Likes,
                    CountFolower = x.Story.CountFolower,
                    CountRate = x.Story.CountRate,
                    HasProgress = x.Progress != null,
                    LastChapterId = x.Progress != null ? x.Progress.LastChapterId : null,
                    LastChapterNumber = x.Progress != null ? x.Progress.ChapterNumber : null,
                    LatestChapters = x.LatestChapters
                })
                .ToListAsync();

            return PartialView("_StoryCardsPartial", stories);
        }
        public async Task<IActionResult> Detail(int id)
        {
            var story = await _context.TblStories
                .Include(s => s.TblChapters)
                .Include(s => s.Author)
                .Include(s => s.TblCategoryOfStories)
                    .ThenInclude(a => a.Category)
                .FirstOrDefaultAsync(s => s.StoryId == id);

            if (story == null)
            {
                return NotFound();
            }

            // 1. Lấy UserId từ Session
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            bool isLiked = false;
            bool isFollowed = false;
            int userRating = 0;

            if (userId > 0)
            {
                // Kiểm tra like
                isLiked = await _context.TblUserLikings
                    .AnyAsync(l => l.UserId == userId && l.StoryId == id && l.Liking > 0);

                // Kiểm tra follow
                isFollowed = await _context.TblUserFollowStories
                    .AnyAsync(f => f.UserId == userId && f.StoryId == id);

        // Lấy số sao đã đánh giá
        userRating = await _context.TblUserRatings
            .Where(r => r.UserId == userId && r.StoryId == id)
            .Select(r => r.Rating)
            .FirstOrDefaultAsync();
    }

            // 2. Truyền các trạng thái qua ViewBag
            ViewBag.IsLiked = isLiked;
            ViewBag.IsFollowed = isFollowed;
            ViewBag.UserRating = userRating;

            // 3. QUAN TRỌNG: Trả về chính đối tượng 'story' (kiểu TblStory) để khớp với @model TblStory của View
            return View(story);
        }

        public async Task<IActionResult> Read(int id)
        {
            // 1. Lấy UserId từ Session
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            // 2. IN LOG TRỰC TIẾP RA CONSOLE / OUTPUT (Không lo bị chặn)
            System.Diagnostics.Debug.WriteLine($"===================> CHECK SESSION: UserId = {userId}, ChapterId = {id}");
            Console.WriteLine($"===================> CHECK SESSION: UserId = {userId}, ChapterId = {id}");

            var chapter = await _context.TblChapters.FirstOrDefaultAsync(c => c.ChapterId == id);
            if (chapter == null)
            {
                Console.WriteLine($"===================> KHÔNG TÌM THẤY CHAPTER: {id}");
                return NotFound();
            }

            if (userId > 0)
            {
                try
                {
                    Console.WriteLine("===================> CHUẨN BỊ GỌI STORED PROCEDURE...");

                    var pUserId = new SqlParameter("@UserID", userId);
                    var pStoryId = new SqlParameter("@StoryID", chapter.StoryId);
                    var pChapterId = new SqlParameter("@ChapterID", id);

                    int rows = await _context.Database.ExecuteSqlRawAsync(
                        "EXEC [dbo].[sp_SaveUserReadingProgress] @UserID, @StoryID, @ChapterID",
                        pUserId, pStoryId, pChapterId
                    );

                    Console.WriteLine($"===================> THỰC THI SP THÀNH CÔNG! Số dòng ảnh hưởng: {rows}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"===================> LỖI SP: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("===================> KẾT QUẢ: UserId = 0 NÊN KHÔNG GỌI STORED PROCEDURE!");
            }

            return View(chapter);
        }
        // ==========================================
        // 1. CHỨC NĂNG LIKE / UNLIKE
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ToggleLike(int storyId)
        {
            try
            {
                int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                if (userId <= 0)
                {
                    return Json(new { success = false, requireLogin = true, message = "Please sign in to like this story!" });
                }

                var story = await _context.TblStories.FirstOrDefaultAsync(s => s.StoryId == storyId);
                if (story == null)
                {
                    return Json(new { success = false, message = "Story not found!" });
                }

                var existingLike = await _context.TblUserLikings
                    .FirstOrDefaultAsync(l => l.UserId == userId && l.StoryId == storyId);

                bool isLiked = false;

                if (existingLike == null)
                {
                    var newLike = new TblUserLiking
                    {
                        UserId = userId,
                        StoryId = storyId,
                        Liking = 1,
                        LikeDate = DateTime.Now
                    };

                    _context.TblUserLikings.Add(newLike);
                    story.Likes = (story.Likes ?? 0) + 1;
                    isLiked = true;
                }
                else
                {
                    if (existingLike.Liking > 0)
                    {
                        existingLike.Liking = 0;
                        story.Likes = Math.Max(0, (story.Likes ?? 0) - 1);
                        isLiked = false;
                    }
                    else
                    {
                        existingLike.Liking = 1;
                        existingLike.LikeDate = DateTime.Now;
                        story.Likes = (story.Likes ?? 0) + 1;
                        isLiked = true;
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    isLiked = isLiked,
                    likesCount = story.Likes ?? 0
                });
            }
            catch (DbUpdateException dbEx)
            {
                var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
                _logger.LogError(dbEx, "Database Error in ToggleLike: {Message}", innerMsg);
                return StatusCode(500, new { success = false, message = "Database Error: " + innerMsg });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ToggleLike: {Message}", ex.Message);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ==========================================
        // 2. CHỨC NĂNG ĐÁNH GIÁ (RATING)
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RateStory(int storyId, int rating)
        {
            try
            {
                int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                if (userId <= 0)
                {
                    return Json(new { success = false, requireLogin = true, message = "Please sign in to rate this story!" });
                }

                if (rating < 1 || rating > 5)
                {
                    return Json(new { success = false, message = "Rating must be between 1 and 5!" });
                }

                var story = await _context.TblStories.FirstOrDefaultAsync(s => s.StoryId == storyId);
                if (story == null)
                {
                    return Json(new { success = false, message = "Story not found!" });
                }

                var existingRating = await _context.TblUserRatings
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.StoryId == storyId);

                if (existingRating == null)
                {
                    var newRating = new TblUserRating
                    {
                        UserId = userId,
                        StoryId = storyId,
                        Rating = rating,
                        RatedDate = DateTime.Now
                    };

                    _context.TblUserRatings.Add(newRating);
                }
                else
                {
                    existingRating.Rating = rating;
                    existingRating.RatedDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // 3. Tính lại điểm trung bình cộng chính xác từ Database
                var allRatings = await _context.TblUserRatings
                    .Where(r => r.StoryId == storyId)
                    .Select(r => r.Rating)
                    .ToListAsync();

                int countRate = allRatings.Count;
                double averageRate = countRate > 0 ? Math.Round(allRatings.Average(), 1) : 0.0;

                story.CountRate = countRate;
                story.Rate = averageRate;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Rated successfully!",
                    userRating = rating,
                    averageRate = averageRate.ToString("0.0"),
                    countRate = countRate
                });
            }
            catch (DbUpdateException dbEx)
            {
                var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
                _logger.LogError(dbEx, "Database Error in RateStory: {Message}", innerMsg);
                return StatusCode(500, new { success = false, message = "Database Error: " + innerMsg });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RateStory: {Message}", ex.Message);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> ToggleFollow(int storyId)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId <= 0)
                return Json(new { success = false, requireLogin = true, message = "Please sign in to follow this story!" });

            var story = await _context.TblStories.FindAsync(storyId);
            if (story == null) return Json(new { success = false, message = "Story not found!" });

            var followRecord = await _context.TblUserFollowStories
                .FirstOrDefaultAsync(f => f.UserId == userId && f.StoryId == storyId); //[cite: 5]

            bool isFollowed = false;
            if (followRecord == null)
            {
                // Chưa theo dõi -> Bấm để theo dõi
                _context.TblUserFollowStories.Add(new TblUserFollowStory //[cite: 5]
                {
                    UserId = userId,
                    StoryId = storyId
                });
                story.CountFolower = (story.CountFolower ?? 0) + 1;
                isFollowed = true;
            }
            else
            {
                // Đang theo dõi -> Hủy theo dõi
                _context.TblUserFollowStories.Remove(followRecord);
                story.CountFolower = Math.Max(0, (story.CountFolower ?? 0) - 1);
                isFollowed = false;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, isFollowed = isFollowed, followersCount = story.CountFolower });
        }
    }
}
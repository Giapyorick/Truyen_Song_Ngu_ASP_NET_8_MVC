using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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

        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> GetStoriesInfinite(string? search, string type = "likes", int? categoryId = null, int page = 1, int pageSize = 8)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var rqCulture = HttpContext.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>();
            var cultureName = rqCulture?.RequestCulture.UICulture.Name
                              ?? Request.Cookies[Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName]
                              ?? System.Globalization.CultureInfo.CurrentUICulture.Name;

            bool isVi = cultureName.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

            var query = _context.TblStories.AsNoTracking().AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(s => s.TblCategoryOfStories.Any(cs => cs.CategoryId == categoryId.Value));
            }

            // 1. Tối ưu tìm kiếm - Chỉ dùng Contains hoặc Like duy nhất 1 lần
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                if (isVi)
                {
                    query = query.Where(s =>
                        s.TblStoryTranslations.Any(t => t.LanguageCode.StartsWith("vi") && t.Title.Contains(search))
                        || (!s.TblStoryTranslations.Any(t => t.LanguageCode.StartsWith("vi")) && s.Title.Contains(search))
                    );
                }
                else
                {
                    query = query.Where(s =>
                        s.Title.Contains(search)
                        || s.TblStoryTranslations.Any(t => t.LanguageCode.StartsWith("en") && t.Title.Contains(search))
                    );
                }
            }

            // 2. Sắp xếp
            query = (type?.ToLower()) switch
            {
                "rate" => query.OrderByDescending(s => s.Rate).ThenByDescending(s => s.CountRate).ThenByDescending(s => s.StoryId),
                "follower" => query.OrderByDescending(s => s.CountFolower).ThenByDescending(s => s.Likes).ThenByDescending(s => s.StoryId),
                "most_rated" => query.OrderByDescending(s => s.CountRate).ThenByDescending(s => s.Rate).ThenByDescending(s => s.StoryId),
                _ => query.OrderByDescending(s => s.Likes).ThenByDescending(s => s.CountFolower).ThenByDescending(s => s.StoryId)
            };

            // 3. Phân trang và Project trực tiếp bằng 1 Query duy nhất
            var stories = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new StoryListViewModel
                {
                    StoryID = s.StoryId,
                    Title = isVi
                        ? (s.TblStoryTranslations
                            .Where(st => st.LanguageCode.StartsWith("vi"))
                            .Select(st => st.Title)
                            .FirstOrDefault() ?? s.Title)
                        : s.Title,
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
                            ChapterTitle = isVi
                                ? (ch.TblChapterTranslations
                                    .Where(ct => ct.LanguageCode.StartsWith("vi"))
                                    .Select(ct => ct.Title)
                                    .FirstOrDefault() ?? ch.Title)
                                : ch.Title
                        }).ToList()
                })
                .ToListAsync();

            return PartialView("_StoryCardsPartial", stories);
        }
        public async Task<IActionResult> Detail(int id)
        {
            // 1. Xác định ngôn ngữ hiện tại của request (en-US / vi-VN)
            var currentCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            bool isVi = currentCulture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
            string langPrefix = isVi ? "vi" : "en";

            var story = await _context.TblStories
                .Include(s => s.TblStoryTranslations)
                .Include(s => s.Author)
                .Include(s => s.TblCategoryOfStories)
                    .ThenInclude(cs => cs.Category)
                        .ThenInclude(c => c.TblCategoryTranslations)
                .FirstOrDefaultAsync(s => s.StoryId == id);

            if (story == null)
            {
                return NotFound();
            }

            // 2. Gán dữ liệu dịch theo ngôn ngữ hiện tại vào Model hoặc ViewBag
            var translatedTitle = story.TblStoryTranslations
                .Where(t => t.LanguageCode.StartsWith(langPrefix))
                .Select(t => t.Title)
                .FirstOrDefault();

            var translatedDesc = story.TblStoryTranslations
                .Where(t => t.LanguageCode.StartsWith(langPrefix))
                .Select(t => t.Description)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(translatedTitle)) story.Title = translatedTitle;
            if (!string.IsNullOrWhiteSpace(translatedDesc)) story.Description = translatedDesc;

            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            bool isLiked = false;
            bool isFollowed = false;
            int userRating = 0;

            if (userId > 0)
            {
                isLiked = await _context.TblUserLikings
                    .AnyAsync(l => l.UserId == userId && l.StoryId == id && l.Liking > 0);

                isFollowed = await _context.TblUserFollowStories
                    .AnyAsync(f => f.UserId == userId && f.StoryId == id);

                userRating = await _context.TblUserRatings
                    .Where(r => r.UserId == userId && r.StoryId == id)
                    .Select(r => r.Rating)
                    .FirstOrDefaultAsync();
            }

            ViewBag.IsLiked = isLiked;
            ViewBag.IsFollowed = isFollowed;
            ViewBag.UserRating = userRating;
            ViewBag.LangPrefix = langPrefix;

            return View(story);
        }

        public async Task<IActionResult> Read(int id)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var chapter = await _context.TblChapters.FirstOrDefaultAsync(c => c.ChapterId == id);
            if (chapter == null)
            {
                return NotFound();
            }

            if (userId > 0)
            {
                try
                {
                    var pUserId = new SqlParameter("@UserID", userId);
                    var pStoryId = new SqlParameter("@StoryID", chapter.StoryId);
                    var pChapterId = new SqlParameter("@ChapterID", id);

                    await _context.Database.ExecuteSqlRawAsync(
                        "EXEC [dbo].[sp_SaveUserReadingProgress] @UserID, @StoryID, @ChapterID",
                        pUserId, pStoryId, pChapterId
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing sp_SaveUserReadingProgress");
                }
            }

            return View(chapter);
        }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ToggleLike: {Message}", ex.Message);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

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
                .FirstOrDefaultAsync(f => f.UserId == userId && f.StoryId == storyId);

            bool isFollowed = false;
            if (followRecord == null)
            {
                _context.TblUserFollowStories.Add(new TblUserFollowStory
                {
                    UserId = userId,
                    StoryId = storyId
                });
                story.CountFolower = (story.CountFolower ?? 0) + 1;
                isFollowed = true;
            }
            else
            {
                _context.TblUserFollowStories.Remove(followRecord);
                story.CountFolower = Math.Max(0, (story.CountFolower ?? 0) - 1);
                isFollowed = false;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, isFollowed = isFollowed, followersCount = story.CountFolower });
        }
    }
}
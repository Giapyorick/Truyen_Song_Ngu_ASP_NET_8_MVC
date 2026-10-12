using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Controllers
{
    public class ProfileController : Controller
    {
        private readonly TruyenSongNguContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProfileController(TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId <= 0)
            {
                return RedirectToAction("Index", "Home");
            }

            var user = await _context.TblUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            // Lấy ngôn ngữ hiện tại từ Culture (Ví dụ: "vi" hoặc "en")
            var cultureCookie = Request.Cookies[".AspNetCore.Culture"];
            bool isVi = string.IsNullOrEmpty(cultureCookie)
                ? CultureInfo.CurrentCulture.Name.StartsWith("vi")
                : cultureCookie.Contains("vi");
            string langCode = isVi ? "vi" : "en";

            // 1. Truy vấn truyện theo dõi (Đã hỗ trợ Song ngữ)
            var followedStories = await _context.TblUserFollowStories
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => f.Story)
                .Select(s => new
                {
                    Story = s,
                    // Ưu tiên lấy tiêu đề từ bản dịch theo ngôn ngữ hiện tại
                    DisplayTitle = isVi
                        ? (s.TblStoryTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi")).Title ?? s.Title)
                        : s.Title,
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
                            ChapterTitle = isVi
                                ? (_context.TblChapterTranslations.FirstOrDefault(ct => ct.ChapterId == ch.ChapterId && ct.LanguageCode.StartsWith("vi")).Title ?? ch.Title)
                                : ch.Title
                        }).ToList()
                })
                .Select(x => new StoryListViewModel
                {
                    StoryID = x.Story.StoryId,
                    Title = x.DisplayTitle,
                    Img = x.Story.Img,
                    Lang = x.Story.Lang,
                    Rate = x.Story.Rate,
                    Likes = x.Story.Likes,
                    CountFolower = x.Story.CountFolower,
                    HasProgress = x.Progress != null,
                    LastChapterId = x.Progress != null ? x.Progress.LastChapterId : null,
                    LastChapterNumber = x.Progress != null ? x.Progress.ChapterNumber : null,
                    LatestChapters = x.LatestChapters
                })
                .ToListAsync();

            // 2. Truy vấn truyện đã like (Đã hỗ trợ Song ngữ)
            var likedStories = await _context.TblUserLikings
                .AsNoTracking()
                .Where(l => l.UserId == userId && l.Liking > 0)
                .Select(l => l.Story)
                .Select(s => new
                {
                    Story = s,
                    DisplayTitle = isVi
                        ? (s.TblStoryTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi")).Title ?? s.Title)
                        : s.Title,
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
                            ChapterTitle = isVi
                                ? (_context.TblChapterTranslations.FirstOrDefault(ct => ct.ChapterId == ch.ChapterId && ct.LanguageCode.StartsWith("vi")).Title ?? ch.Title)
                                : ch.Title
                        }).ToList()
                })
                .Select(x => new StoryListViewModel
                {
                    StoryID = x.Story.StoryId,
                    Title = x.DisplayTitle,
                    Img = x.Story.Img,
                    Lang = x.Story.Lang,
                    Rate = x.Story.Rate,
                    Likes = x.Story.Likes,
                    CountFolower = x.Story.CountFolower,
                    HasProgress = x.Progress != null,
                    LastChapterId = x.Progress != null ? x.Progress.LastChapterId : null,
                    LastChapterNumber = x.Progress != null ? x.Progress.ChapterNumber : null,
                    LatestChapters = x.LatestChapters
                })
                .ToListAsync();

            // 3. Truy vấn truyện đã đọc (Đã hỗ trợ Song ngữ)
            var readStories = await _context.TblUserReadingProgresses
                .AsNoTracking()
                .Where(rp => rp.UserId == userId)
                .OrderByDescending(rp => rp.UpdatedAt)
                .Join(_context.TblStories,
                      rp => rp.StoryId,
                      s => s.StoryId,
                      (rp, s) => new { Progress = rp, Story = s })
                .Select(x => new
                {
                    Story = x.Story,
                    DisplayTitle = isVi
                        ? (x.Story.TblStoryTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi")).Title ?? x.Story.Title)
                        : x.Story.Title,
                    Progress = new
                    {
                        x.Progress.LastChapterId,
                        ChapterNumber = _context.TblChapters
                            .Where(c => c.ChapterId == x.Progress.LastChapterId)
                            .Select(c => (int?)c.ChapterNumber)
                            .FirstOrDefault()
                    },
                    LatestChapters = _context.TblChapters
                        .Where(ch => ch.StoryId == x.Story.StoryId)
                        .OrderByDescending(ch => ch.ChapterNumber)
                        .Take(3)
                        .Select(ch => new LatestChapterItemViewModel
                        {
                            ChapterId = ch.ChapterId,
                            ChapterNumber = ch.ChapterNumber,
                            ChapterTitle = isVi
                                ? (_context.TblChapterTranslations.FirstOrDefault(ct => ct.ChapterId == ch.ChapterId && ct.LanguageCode.StartsWith("vi")).Title ?? ch.Title)
                                : ch.Title
                        }).ToList()
                })
                .Select(x => new StoryListViewModel
                {
                    StoryID = x.Story.StoryId,
                    Title = x.DisplayTitle,
                    Img = x.Story.Img,
                    Lang = x.Story.Lang,
                    Rate = x.Story.Rate,
                    Likes = x.Story.Likes,
                    CountFolower = x.Story.CountFolower,
                    HasProgress = true,
                    LastChapterId = x.Progress.LastChapterId,
                    LastChapterNumber = x.Progress.ChapterNumber,
                    LatestChapters = x.LatestChapters
                })
                .ToListAsync();

            // 4. Truy vấn danh sách từ vựng cá nhân (Đã hỗ trợ Song ngữ tiêu đề truyện)
            var vocabularies = await _context.TblUserVocabularies
                .AsNoTracking()
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new UserVocabItemViewModel
                {
                    VocabId = v.VocabId,
                    WordOrPhrase = v.WordOrPhrase,
                    Explanation = v.Explanation,
                    ContextSentence = v.ContextSentence,
                    StoryTitle = _context.TblChapters
                        .Where(c => c.ChapterId == v.ChapterId)
                        .Select(c => isVi
                            ? (c.Story.TblStoryTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi")).Title ?? c.Story.Title)
                            : c.Story.Title)
                        .FirstOrDefault(),
                    ChapterNumber = _context.TblChapters
                        .Where(c => c.ChapterId == v.ChapterId)
                        .Select(c => (int?)c.ChapterNumber)
                        .FirstOrDefault(),
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync();

            var viewModel = new ProfileViewModel
            {
                User = user,
                FollowedStories = followedStories,
                LikedStories = likedStories,
                ReadStories = readStories,
                Vocabularies = vocabularies
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(UsersViewModel model)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId <= 0)
            {
                return Json(new { success = false, message = "Please sign in to update profile!" });
            }

            var user = await _context.TblUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User account not found!" });
            }

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                string cleanEmail = model.Email.Trim().ToLower();
                bool emailUsed = await _context.TblUsers
                    .AnyAsync(u => u.UserId != userId && u.Email != null && u.Email.ToLower() == cleanEmail);

                if (emailUsed)
                {
                    return Json(new { success = false, message = "This email is already in use by another account!" });
                }
                user.Email = cleanEmail;
            }

            if (!string.IsNullOrWhiteSpace(model.Name)) user.Name = model.Name.Trim();
            user.Phone = model.Phone?.Trim();
            user.DoB = model.DoB;
            user.Gender = model.Gender ?? "Male";

            if (!string.IsNullOrWhiteSpace(model.Passwork))
            {
                if (model.Passwork.Trim().Length < 6)
                {
                    return Json(new { success = false, message = "New password must be at least 6 characters!" });
                }
                user.Passwork = PasswordHasher.Hash(model.Passwork.Trim());
            }

            if (model.formFile != null && model.formFile.Length > 0)
            {
                string folder = Path.Combine(_webHostEnvironment.WebRootPath, "assets", "image", "users");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = $"User_{Guid.NewGuid()}{Path.GetExtension(model.formFile.FileName)}";
                string fullPath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await model.formFile.CopyToAsync(stream);
                }

                if (!string.IsNullOrEmpty(user.Img))
                {
                    string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, user.Img.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                user.Img = $"assets/image/users/{fileName}";
                HttpContext.Session.SetString("UserAvatar", user.Img);
            }

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("UserName", user.Name ?? "Reader");
            HttpContext.Session.SetString("UserEmail", user.Email ?? "");

            return Json(new { success = true, message = "Profile updated successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteVocabulary(int id)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thao tác!" });
            }

            var vocab = await _context.TblUserVocabularies
                .FirstOrDefaultAsync(v => v.VocabId == id && v.UserId == userId);

            if (vocab == null)
            {
                return Json(new { success = false, message = "Từ vựng không tồn tại hoặc đã bị xóa!" });
            }

            _context.TblUserVocabularies.Remove(vocab);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa từ vựng khỏi sổ tay!" });
        }
    }
}
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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

            // 1. Truy vấn truyện theo dõi
            var followedStories = await _context.TblUserFollowStories
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => f.Story)
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
                    HasProgress = x.Progress != null,
                    LastChapterId = x.Progress != null ? x.Progress.LastChapterId : null,
                    LastChapterNumber = x.Progress != null ? x.Progress.ChapterNumber : null,
                    LatestChapters = x.LatestChapters
                })
                .ToListAsync();

            // 2. Truy vấn truyện đã like
            var likedStories = await _context.TblUserLikings
                .AsNoTracking()
                .Where(l => l.UserId == userId && l.Liking > 0)
                .Select(l => l.Story)
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
                    HasProgress = x.Progress != null,
                    LastChapterId = x.Progress != null ? x.Progress.LastChapterId : null,
                    LastChapterNumber = x.Progress != null ? x.Progress.ChapterNumber : null,
                    LatestChapters = x.LatestChapters
                })
                .ToListAsync();

            // 3. BỔ SUNG: Truy vấn truyện đã đọc (Lấy từ TblUserReadingProgress)
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
                    HasProgress = true, // Truyện trong ReadingProgress chắc chắn đã có tiến độ đọc
                    LastChapterId = x.Progress.LastChapterId,
                    LastChapterNumber = x.Progress.ChapterNumber,
                    LatestChapters = x.LatestChapters
                })
                .ToListAsync();

            var viewModel = new ProfileViewModel
            {
                User = user,
                FollowedStories = followedStories,
                LikedStories = likedStories,
                ReadStories = readStories
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

            // 1. Kiểm tra Email nếu có thay đổi
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

            // 2. Cập nhật thông tin cơ bản
            if (!string.IsNullOrWhiteSpace(model.Name)) user.Name = model.Name.Trim();
            user.Phone = model.Phone?.Trim();
            user.DoB = model.DoB;
            user.Gender = model.Gender ?? "Male";

            // 3. Đổi mật khẩu nếu người dùng nhập mật khẩu mới
            if (!string.IsNullOrWhiteSpace(model.Passwork))
            {
                if (model.Passwork.Trim().Length < 6)
                {
                    return Json(new { success = false, message = "New password must be at least 6 characters!" });
                }
                user.Passwork = PasswordHasher.Hash(model.Passwork.Trim());
            }

            // 4. Xử lý ảnh đại diện nếu có tải file mới
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

                // Xóa ảnh cũ (nếu có)
                if (!string.IsNullOrEmpty(user.Img))
                {
                    string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, user.Img.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                user.Img = $"assets/image/users/{fileName}";
                HttpContext.Session.SetString("UserAvatar", user.Img);
            }

            await _context.SaveChangesAsync();

            // Cập nhật lại Session hiển thị
            HttpContext.Session.SetString("UserName", user.Name ?? "Reader");
            HttpContext.Session.SetString("UserEmail", user.Email ?? "");

            return Json(new { success = true, message = "Profile updated successfully!" });
        }
    }
}
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.Controllers
{
    /// <summary>
    /// Bình luận cho từng truyện (trang Stories/Detail). Bảng riêng dbo.tblStoryComment
    /// (script StoreProcedure/StoryComment.sql) – không dùng tblComment cũ vì bảng đó không khớp CSDL thật.
    ///   GET  /StoryComments/List?storyId=1&amp;page=1   -> danh sách (mới nhất trước)
    ///   POST /StoryComments/Add     { storyId, contents } -> cần đăng nhập
    ///   POST /StoryComments/Delete  { id }                -> chủ bình luận hoặc Admin (không phải Viewer)
    /// Nội dung trả về dạng văn bản thô; phía JS (story-comments.js) tự escape khi hiển thị.
    /// </summary>
    public class StoryCommentsController : Controller
    {
        private const int PageSize = 10;
        private const int MinLength = 2;
        private const int MaxLength = 1000;
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(20);   // chống spam: 1 bình luận / 20 giây
        private const int MaxPerHour = 30;

        private readonly TruyenSongNguContext _context;

        public StoryCommentsController(TruyenSongNguContext context)
        {
            _context = context;
        }

        private bool IsVi()
        {
            var name = System.Globalization.CultureInfo.CurrentUICulture.Name;
            return name.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
        }

        private string T(string vi, string en) => IsVi() ? vi : en;

        private int CurrentUserId => HttpContext.Session.GetInt32("UserId") ?? 0;

        // Admin / Super Admin đang đăng nhập trang quản trị (cùng trình duyệt) được xóa mọi bình luận
        private bool IsModerator()
        {
            var role = HttpContext.Session.GetString("AdminRole")?.Trim() ?? "";
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "Super Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        }

        public class AddCommentRequest
        {
            public int StoryId { get; set; }
            public string? Contents { get; set; }
        }

        public class DeleteCommentRequest
        {
            public int Id { get; set; }
        }

        // ------------------------------------------------------------------
        // Danh sách bình luận
        // ------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> List(int storyId, int page = 1)
        {
            if (storyId <= 0) return Json(new { success = false, total = 0, items = Array.Empty<object>() });
            if (page < 1) page = 1;

            int userId = CurrentUserId;
            bool isMod = IsModerator();

            var query = _context.TblStoryComments.AsNoTracking().Where(c => c.StoryId == storyId);
            int total = await query.CountAsync();

            var rows = await query
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.StoryCommentId)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(c => new
                {
                    c.StoryCommentId,
                    c.UserId,
                    c.UserName,
                    c.Contents,
                    c.CreatedAt,
                    UserImg = _context.TblUsers.Where(u => u.UserId == c.UserId).Select(u => u.Img).FirstOrDefault(),
                    CurrentName = _context.TblUsers.Where(u => u.UserId == c.UserId).Select(u => u.Name).FirstOrDefault()
                })
                .ToListAsync();

            var items = rows.Select(c => new
            {
                id = c.StoryCommentId,
                userName = !string.IsNullOrWhiteSpace(c.CurrentName) ? c.CurrentName
                         : (!string.IsNullOrWhiteSpace(c.UserName) ? c.UserName : T("Độc giả", "Reader")),
                userImg = c.UserImg,
                contents = c.Contents,
                createdAt = new DateTimeOffset(DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Local)).ToUnixTimeMilliseconds(),
                canDelete = isMod || (userId > 0 && c.UserId == userId),
                isMine = userId > 0 && c.UserId == userId
            });

            return Json(new
            {
                success = true,
                total,
                page,
                pageSize = PageSize,
                hasMore = page * PageSize < total,
                items
            });
        }

        // ------------------------------------------------------------------
        // Gửi bình luận
        // ------------------------------------------------------------------
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Add([FromBody] AddCommentRequest req)
        {
            int userId = CurrentUserId;
            if (userId <= 0)
                return Json(new { success = false, requireLogin = true, message = T("Vui lòng đăng nhập để bình luận.", "Please sign in to comment.") });

            if (req == null || req.StoryId <= 0)
                return Json(new { success = false, message = T("Dữ liệu không hợp lệ.", "Invalid request.") });

            // Chuẩn hóa: bỏ ký tự điều khiển, gộp nhiều dòng trống, cắt khoảng trắng hai đầu
            var contents = (req.Contents ?? "").Replace("\r\n", "\n");
            contents = Regex.Replace(contents, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F​-‍﻿]", "");
            contents = Regex.Replace(contents, @"\n{3,}", "\n\n").Trim();

            if (contents.Length < MinLength)
                return Json(new { success = false, message = T("Bình luận quá ngắn.", "Your comment is too short.") });
            if (contents.Length > MaxLength)
                return Json(new { success = false, message = T($"Bình luận tối đa {MaxLength} ký tự.", $"Comments are limited to {MaxLength} characters.") });

            bool storyExists = await _context.TblStories.AnyAsync(s => s.StoryId == req.StoryId);
            if (!storyExists)
                return Json(new { success = false, message = T("Không tìm thấy truyện.", "Story not found.") });

            // Chống spam
            var now = DateTime.Now;
            var recent = await _context.TblStoryComments.AsNoTracking()
                .Where(c => c.UserId == userId && c.CreatedAt >= now.AddHours(-1))
                .Select(c => new { c.CreatedAt, c.StoryId, c.Contents })
                .ToListAsync();

            var last = recent.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
            if (last != null && now - last.CreatedAt < MinInterval)
                return Json(new { success = false, message = T("Bạn bình luận nhanh quá, chờ vài giây nhé.", "You're commenting too fast, please wait a moment.") });
            if (recent.Count >= MaxPerHour)
                return Json(new { success = false, message = T("Bạn đã bình luận quá nhiều trong 1 giờ.", "You've posted too many comments this hour.") });
            if (recent.Any(c => c.StoryId == req.StoryId && string.Equals(c.Contents?.Trim(), contents, StringComparison.OrdinalIgnoreCase)))
                return Json(new { success = false, message = T("Bạn vừa gửi bình luận này rồi.", "You just posted this comment.") });

            var user = await _context.TblUsers.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.Name, u.Img })
                .FirstOrDefaultAsync();
            if (user == null)
                return Json(new { success = false, requireLogin = true, message = T("Vui lòng đăng nhập lại.", "Please sign in again.") });

            var name = !string.IsNullOrWhiteSpace(user.Name) ? user.Name!.Trim() : (HttpContext.Session.GetString("UserName") ?? T("Độc giả", "Reader"));
            if (name.Length > 100) name = name.Substring(0, 100);

            var comment = new TblStoryComment
            {
                StoryId = req.StoryId,
                UserId = userId,
                UserName = name,
                Contents = contents,
                CreatedAt = now
            };
            _context.TblStoryComments.Add(comment);
            await _context.SaveChangesAsync();

            int total = await _context.TblStoryComments.CountAsync(c => c.StoryId == req.StoryId);

            return Json(new
            {
                success = true,
                total,
                item = new
                {
                    id = comment.StoryCommentId,
                    userName = name,
                    userImg = user.Img,
                    contents,
                    createdAt = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Local)).ToUnixTimeMilliseconds(),
                    canDelete = true,
                    isMine = true
                }
            });
        }

        // ------------------------------------------------------------------
        // Xóa bình luận
        // ------------------------------------------------------------------
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Delete([FromBody] DeleteCommentRequest req)
        {
            int userId = CurrentUserId;
            bool isMod = IsModerator();
            if (userId <= 0 && !isMod)
                return Json(new { success = false, requireLogin = true, message = T("Vui lòng đăng nhập.", "Please sign in.") });

            var comment = await _context.TblStoryComments.FirstOrDefaultAsync(c => c.StoryCommentId == (req != null ? req.Id : 0));
            if (comment == null)
                return Json(new { success = false, message = T("Bình luận không còn tồn tại.", "This comment no longer exists.") });

            if (!isMod && comment.UserId != userId)
                return Json(new { success = false, message = T("Bạn không thể xóa bình luận của người khác.", "You can't delete someone else's comment.") });

            int storyId = comment.StoryId;
            _context.TblStoryComments.Remove(comment);
            await _context.SaveChangesAsync();

            int total = await _context.TblStoryComments.CountAsync(c => c.StoryId == storyId);
            return Json(new { success = true, total });
        }
    }
}

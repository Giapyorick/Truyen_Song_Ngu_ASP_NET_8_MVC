using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Hubs;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    /// <summary>
    /// ADMIN > KIỂM DUYỆT CỘNG ĐỒNG
    /// Xem báo cáo vi phạm, ẩn / hiện lại / xóa bài viết (tblPosts) và bình luận (tblPostComments).
    /// Lưu ý: TblPost / TblPostComment có bộ lọc toàn cục ẩn nội dung IsHidden (TruyenSongNguContext),
    /// nên mọi truy vấn ở đây đều dùng IgnoreQueryFilters() để thấy cả nội dung đã ẩn.
    /// Xem: Views/CommunityModeration/Index.cshtml, wwwroot/js/admin/moderation/moderation.js
    /// </summary>
    [Area("Admin")]
    [AdminAuthorize]
    public class CommunityModerationController : Controller
    {
        private readonly TruyenSongNguContext _context;
        private readonly IHubContext<CommunityHub> _hubContext;

        public CommunityModerationController(TruyenSongNguContext context, IHubContext<CommunityHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private bool IsVi => CultureInfo.CurrentUICulture.Name.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
        private string T(string vi, string en) => IsVi ? vi : en;
        private int? CurrentAdminId => HttpContext.Session.GetInt32("AdminId");

        private static string Snippet(string? text, int max = 220)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        private IQueryable<TblPost> Posts => _context.TblPosts.IgnoreQueryFilters();
        private IQueryable<TblPostComment> Comments => _context.TblPostComments.IgnoreQueryFilters();

        // ======================================================================
        // 1. TRANG CHÍNH + THỐNG KÊ
        // ======================================================================
        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> Stats()
        {
            var pendingTargets = await _context.TblContentReports
                .Where(r => r.Status == TblContentReport.StatusPending)
                .Select(r => new { r.TargetType, r.TargetId })
                .Distinct()
                .CountAsync();

            return Json(new
            {
                pendingReports = pendingTargets,
                totalPosts = await Posts.CountAsync(),
                hiddenPosts = await Posts.CountAsync(p => p.IsHidden),
                totalComments = await Comments.CountAsync(),
                hiddenComments = await Comments.CountAsync(c => c.IsHidden)
            });
        }

        // Danh sách truyện cho ô lọc
        [HttpGet]
        public async Task<IActionResult> Stories()
        {
            var stories = await _context.TblStories.AsNoTracking()
                .OrderBy(s => s.Title)
                .Select(s => new { id = s.StoryId, title = s.Title })
                .ToListAsync();
            return Json(stories);
        }

        // ======================================================================
        // 2. BÁO CÁO VI PHẠM (gom theo nội dung bị báo cáo)
        // ======================================================================
        [HttpGet]
        public async Task<IActionResult> Reports(string status = "pending", int page = 1, int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);
            if (status != TblContentReport.StatusResolved && status != TblContentReport.StatusDismissed)
                status = TblContentReport.StatusPending;

            var grouped = _context.TblContentReports
                .Where(r => r.Status == status)
                .GroupBy(r => new { r.TargetType, r.TargetId })
                .Select(g => new
                {
                    g.Key.TargetType,
                    g.Key.TargetId,
                    Count = g.Count(),
                    LastAt = g.Max(r => r.CreatedAt)
                });

            int total = await grouped.CountAsync();
            var groups = await grouped
                .OrderByDescending(g => g.Count).ThenByDescending(g => g.LastAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            // Lý do + ghi chú của các báo cáo trong trang hiện tại
            var postIds = groups.Where(g => g.TargetType == TblContentReport.TypePost).Select(g => g.TargetId).ToList();
            var commentIds = groups.Where(g => g.TargetType == TblContentReport.TypeComment).Select(g => g.TargetId).ToList();

            var details = await _context.TblContentReports.AsNoTracking()
                .Where(r => r.Status == status &&
                            ((r.TargetType == TblContentReport.TypePost && postIds.Contains(r.TargetId)) ||
                             (r.TargetType == TblContentReport.TypeComment && commentIds.Contains(r.TargetId))))
                .Select(r => new { r.TargetType, r.TargetId, r.Reason, r.Note, r.CreatedAt, Reporter = r.Reporter != null ? r.Reporter.Name : null })
                .ToListAsync();

            var posts = await Posts.AsNoTracking()
                .Where(p => postIds.Contains(p.PostId))
                .Select(p => new { p.PostId, p.Content, p.IsHidden, p.HiddenReason, p.CreatedAt, p.UserId, UserName = p.User != null ? p.User.Name : null, UserImg = p.User != null ? p.User.Img : null, StoryTitle = p.Story != null ? p.Story.Title : null })
                .ToListAsync();

            var comments = await Comments.AsNoTracking()
                .Where(c => commentIds.Contains(c.CommentId))
                .Select(c => new { c.CommentId, c.PostId, c.Content, c.IsHidden, c.HiddenReason, c.CreatedAt, c.UserId, UserName = c.User != null ? c.User.Name : null, UserImg = c.User != null ? c.User.Img : null, StoryTitle = c.Story != null ? c.Story.Title : null })
                .ToListAsync();

            var items = groups.Select(g =>
            {
                var reasons = details.Where(d => d.TargetType == g.TargetType && d.TargetId == g.TargetId).ToList();
                object? target = null;
                if (g.TargetType == TblContentReport.TypePost)
                {
                    var p = posts.FirstOrDefault(x => x.PostId == g.TargetId);
                    if (p != null)
                        target = new { content = Snippet(p.Content), p.IsHidden, p.HiddenReason, createdAt = p.CreatedAt.ToString("dd/MM/yyyy HH:mm"), p.UserId, userName = p.UserName ?? T("Ẩn danh", "Anonymous"), userImg = p.UserImg, storyTitle = p.StoryTitle, postId = p.PostId };
                }
                else
                {
                    var c = comments.FirstOrDefault(x => x.CommentId == g.TargetId);
                    if (c != null)
                        target = new { content = Snippet(c.Content), c.IsHidden, c.HiddenReason, createdAt = c.CreatedAt.ToString("dd/MM/yyyy HH:mm"), c.UserId, userName = c.UserName ?? T("Ẩn danh", "Anonymous"), userImg = c.UserImg, storyTitle = c.StoryTitle, postId = c.PostId };
                }

                return new
                {
                    targetType = g.TargetType,
                    targetId = g.TargetId,
                    count = g.Count,
                    lastAt = g.LastAt.ToString("dd/MM/yyyy HH:mm"),
                    reasons = reasons.GroupBy(r => r.Reason).Select(r => new { reason = r.Key, count = r.Count() }).OrderByDescending(r => r.count).ToList(),
                    notes = reasons.Where(r => !string.IsNullOrWhiteSpace(r.Note)).OrderByDescending(r => r.CreatedAt).Take(3)
                                   .Select(r => new { note = r.Note, reporter = r.Reporter ?? T("Ẩn danh", "Anonymous") }).ToList(),
                    target,
                    exists = target != null
                };
            }).ToList();

            return Json(new { total, page, pageSize, items });
        }

        // ======================================================================
        // 3. DANH SÁCH BÀI VIẾT
        // ======================================================================
        [HttpGet]
        public async Task<IActionResult> PostList(string? search, int? storyId, string visibility = "all", int page = 1, int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            var q = Posts.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                string k = search.Trim();
                q = q.Where(p => p.Content.Contains(k) || (p.User != null && p.User.Name != null && p.User.Name.Contains(k)));
            }
            if (storyId.HasValue && storyId > 0) q = q.Where(p => p.StoryId == storyId);
            if (visibility == "hidden") q = q.Where(p => p.IsHidden);
            else if (visibility == "visible") q = q.Where(p => !p.IsHidden);

            int total = await q.CountAsync();
            var rows = await q.OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new
                {
                    p.PostId,
                    p.Content,
                    p.IsHidden,
                    p.HiddenReason,
                    p.HiddenAt,
                    p.IsSpoiler,
                    p.CreatedAt,
                    p.UserId,
                    UserName = p.User != null ? p.User.Name : null,
                    UserImg = p.User != null ? p.User.Img : null,
                    StoryTitle = p.Story != null ? p.Story.Title : null,
                    Likes = p.PostLikes.Count,
                    // Truy vấn gốc đã IgnoreQueryFilters() nên truy vấn con cũng đếm cả nội dung đã ẩn
                    CommentCount = _context.TblPostComments.Count(c => c.PostId == p.PostId),
                    PendingReports = _context.TblContentReports.Count(r => r.TargetType == TblContentReport.TypePost && r.TargetId == p.PostId && r.Status == TblContentReport.StatusPending)
                })
                .ToListAsync();

            var items = rows.Select(p => new
            {
                id = p.PostId,
                content = Snippet(p.Content, 400),
                isHidden = p.IsHidden,
                hiddenReason = p.HiddenReason,
                hiddenAt = p.HiddenAt?.ToString("dd/MM/yyyy HH:mm"),
                isSpoiler = p.IsSpoiler,
                createdAt = p.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                userId = p.UserId,
                userName = p.UserName ?? T("Ẩn danh", "Anonymous"),
                userImg = p.UserImg,
                storyTitle = p.StoryTitle,
                likes = p.Likes,
                comments = p.CommentCount,
                pendingReports = p.PendingReports
            });

            return Json(new { total, page, pageSize, items });
        }

        // ======================================================================
        // 4. DANH SÁCH BÌNH LUẬN
        // ======================================================================
        [HttpGet]
        public async Task<IActionResult> CommentList(string? search, int? storyId, string visibility = "all", int page = 1, int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            var q = Comments.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                string k = search.Trim();
                q = q.Where(c => c.Content.Contains(k) || (c.User != null && c.User.Name != null && c.User.Name.Contains(k)));
            }
            // Lọc theo truyện: truyện gắn vào bình luận hoặc vào bài viết chứa bình luận
            if (storyId.HasValue && storyId > 0)
                q = q.Where(c => c.StoryId == storyId || _context.TblPosts.Any(p => p.PostId == c.PostId && p.StoryId == storyId));
            if (visibility == "hidden") q = q.Where(c => c.IsHidden);
            else if (visibility == "visible") q = q.Where(c => !c.IsHidden);

            int total = await q.CountAsync();
            var rows = await q.OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    c.CommentId,
                    c.PostId,
                    c.ParentCommentId,
                    c.Content,
                    c.IsHidden,
                    c.HiddenReason,
                    c.HiddenAt,
                    c.IsSpoiler,
                    c.CreatedAt,
                    c.UserId,
                    UserName = c.User != null ? c.User.Name : null,
                    UserImg = c.User != null ? c.User.Img : null,
                    StoryTitle = c.Story != null ? c.Story.Title : null,
                    PostContent = _context.TblPosts.Where(p => p.PostId == c.PostId).Select(p => p.Content).FirstOrDefault(),
                    Replies = _context.TblPostComments.Count(r => r.ParentCommentId == c.CommentId),
                    PendingReports = _context.TblContentReports.Count(r => r.TargetType == TblContentReport.TypeComment && r.TargetId == c.CommentId && r.Status == TblContentReport.StatusPending)
                })
                .ToListAsync();

            var items = rows.Select(c => new
            {
                id = c.CommentId,
                postId = c.PostId,
                isReply = c.ParentCommentId.HasValue,
                content = Snippet(c.Content, 400),
                isHidden = c.IsHidden,
                hiddenReason = c.HiddenReason,
                hiddenAt = c.HiddenAt?.ToString("dd/MM/yyyy HH:mm"),
                isSpoiler = c.IsSpoiler,
                createdAt = c.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                userId = c.UserId,
                userName = c.UserName ?? T("Ẩn danh", "Anonymous"),
                userImg = c.UserImg,
                storyTitle = c.StoryTitle,
                postSnippet = Snippet(c.PostContent, 90),
                replies = c.Replies,
                pendingReports = c.PendingReports
            });

            return Json(new { total, page, pageSize, items });
        }

        // ======================================================================
        // 5. ẨN / HIỆN LẠI
        // ======================================================================
        public class ModerationRequest
        {
            public string Type { get; set; } = "";     // "post" | "comment"
            public int Id { get; set; }
            public bool Hidden { get; set; }          // dùng cho SetHidden
            public string? Reason { get; set; }       // lý do ẩn (hiển thị cho admin)
        }

        /// <summary>Lấy id của bình luận và toàn bộ phản hồi con cháu</summary>
        private async Task<List<int>> CollectCommentTree(int rootId)
        {
            var all = new List<int> { rootId };
            var frontier = new List<int> { rootId };
            while (frontier.Count > 0)
            {
                var children = await Comments.Where(c => c.ParentCommentId.HasValue && frontier.Contains(c.ParentCommentId.Value))
                                             .Select(c => c.CommentId).ToListAsync();
                children = children.Except(all).ToList();
                all.AddRange(children);
                frontier = children;
            }
            return all;
        }

        private async Task MarkReports(string type, IEnumerable<int> ids, string newStatus)
        {
            var idList = ids.ToList();
            var reports = await _context.TblContentReports
                .Where(r => r.TargetType == type && idList.Contains(r.TargetId) && r.Status == TblContentReport.StatusPending)
                .ToListAsync();
            foreach (var r in reports)
            {
                r.Status = newStatus;
                r.ResolvedAt = DateTime.Now;
                r.ResolvedByAdminId = CurrentAdminId;
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> SetHidden([FromBody] ModerationRequest req)
        {
            if (req == null || req.Id <= 0) return Json(new { success = false, message = T("Yêu cầu không hợp lệ.", "Invalid request.") });
            string? reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
            if (reason != null && reason.Length > 200) reason = reason.Substring(0, 200);
            var now = DateTime.Now;

            if (req.Type == TblContentReport.TypePost)
            {
                var post = await Posts.FirstOrDefaultAsync(p => p.PostId == req.Id);
                if (post == null) return Json(new { success = false, message = T("Bài viết không tồn tại.", "Post not found.") });

                post.IsHidden = req.Hidden;
                post.HiddenAt = req.Hidden ? now : null;
                post.HiddenReason = req.Hidden ? reason : null;
                if (req.Hidden) await MarkReports(TblContentReport.TypePost, new[] { post.PostId }, TblContentReport.StatusResolved);
                await _context.SaveChangesAsync();

                // Người đang mở trang Cộng đồng: bài biến mất ngay
                if (req.Hidden) await _hubContext.Clients.All.SendAsync("PostDeleted", post.PostId);

                return Json(new
                {
                    success = true,
                    message = req.Hidden ? T("Đã ẩn bài viết.", "Post hidden.") : T("Đã hiện lại bài viết.", "Post restored.")
                });
            }

            if (req.Type == TblContentReport.TypeComment)
            {
                var root = await Comments.FirstOrDefaultAsync(c => c.CommentId == req.Id);
                if (root == null) return Json(new { success = false, message = T("Bình luận không tồn tại.", "Comment not found.") });

                var ids = await CollectCommentTree(root.CommentId);
                var tree = await Comments.Where(c => ids.Contains(c.CommentId)).ToListAsync();

                if (req.Hidden)
                {
                    // Ẩn bình luận và các phản hồi bên dưới (đang hiện) với cùng thời điểm
                    foreach (var c in tree.Where(c => !c.IsHidden || c.CommentId == root.CommentId))
                    {
                        c.IsHidden = true;
                        c.HiddenAt = now;
                        c.HiddenReason = c.CommentId == root.CommentId ? reason : T("Ẩn theo bình luận gốc", "Hidden with parent comment");
                    }
                    await MarkReports(TblContentReport.TypeComment, ids, TblContentReport.StatusResolved);
                }
                else
                {
                    // Hiện lại bình luận + những phản hồi đã bị ẩn CÙNG LÚC với nó
                    var stamp = root.HiddenAt;
                    foreach (var c in tree.Where(c => c.CommentId == root.CommentId || (stamp.HasValue && c.HiddenAt == stamp)))
                    {
                        c.IsHidden = false;
                        c.HiddenAt = null;
                        c.HiddenReason = null;
                    }
                }
                await _context.SaveChangesAsync();

                if (req.Hidden)
                {
                    int totalComments = await _context.TblPostComments.CountAsync(x => x.PostId == root.PostId); // đã lọc ẩn
                    await _hubContext.Clients.All.SendAsync("CommentDeleted", new { postId = root.PostId, deletedCommentIds = ids, totalComments });
                }

                return Json(new
                {
                    success = true,
                    message = req.Hidden
                        ? (ids.Count > 1 ? T($"Đã ẩn bình luận và {ids.Count - 1} phản hồi.", $"Comment and {ids.Count - 1} replies hidden.") : T("Đã ẩn bình luận.", "Comment hidden."))
                        : T("Đã hiện lại bình luận.", "Comment restored.")
                });
            }

            return Json(new { success = false, message = T("Loại nội dung không hợp lệ.", "Invalid content type.") });
        }

        // ======================================================================
        // 6. XÓA VĨNH VIỄN
        // ======================================================================
        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Delete([FromBody] ModerationRequest req)
        {
            if (req == null || req.Id <= 0) return Json(new { success = false, message = T("Yêu cầu không hợp lệ.", "Invalid request.") });

            if (req.Type == TblContentReport.TypePost)
            {
                var post = await Posts.Include(p => p.PostLikes).FirstOrDefaultAsync(p => p.PostId == req.Id);
                if (post == null) return Json(new { success = false, message = T("Bài viết không tồn tại.", "Post not found.") });

                var comments = await Comments.Where(c => c.PostId == post.PostId).ToListAsync();
                var commentIds = comments.Select(c => c.CommentId).ToList();

                // Bỏ liên kết trả lời trước để xóa không vướng khóa ngoại tự tham chiếu
                foreach (var c in comments) c.ParentCommentId = null;
                await _context.SaveChangesAsync();

                _context.TblContentReports.RemoveRange(_context.TblContentReports.Where(r =>
                    (r.TargetType == TblContentReport.TypePost && r.TargetId == post.PostId) ||
                    (r.TargetType == TblContentReport.TypeComment && commentIds.Contains(r.TargetId))));
                _context.TblPostComments.RemoveRange(comments);
                _context.TblPostLikes.RemoveRange(post.PostLikes);
                _context.TblPosts.Remove(post);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("PostDeleted", req.Id);
                return Json(new { success = true, message = T("Đã xóa vĩnh viễn bài viết.", "Post permanently deleted.") });
            }

            if (req.Type == TblContentReport.TypeComment)
            {
                var root = await Comments.FirstOrDefaultAsync(c => c.CommentId == req.Id);
                if (root == null) return Json(new { success = false, message = T("Bình luận không tồn tại.", "Comment not found.") });

                int postId = root.PostId;
                var ids = await CollectCommentTree(root.CommentId);
                var tree = await Comments.Where(c => ids.Contains(c.CommentId)).ToListAsync();

                foreach (var c in tree) c.ParentCommentId = null;
                await _context.SaveChangesAsync();

                _context.TblContentReports.RemoveRange(_context.TblContentReports.Where(r =>
                    r.TargetType == TblContentReport.TypeComment && ids.Contains(r.TargetId)));
                _context.TblPostComments.RemoveRange(tree);
                await _context.SaveChangesAsync();

                int totalComments = await _context.TblPostComments.CountAsync(x => x.PostId == postId);
                await _hubContext.Clients.All.SendAsync("CommentDeleted", new { postId, deletedCommentIds = ids, totalComments });

                return Json(new
                {
                    success = true,
                    message = ids.Count > 1
                        ? T($"Đã xóa bình luận và {ids.Count - 1} phản hồi.", $"Comment and {ids.Count - 1} replies deleted.")
                        : T("Đã xóa vĩnh viễn bình luận.", "Comment permanently deleted.")
                });
            }

            return Json(new { success = false, message = T("Loại nội dung không hợp lệ.", "Invalid content type.") });
        }

        // ======================================================================
        // 7. BỎ QUA BÁO CÁO (nội dung không vi phạm)
        // ======================================================================
        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> DismissReports([FromBody] ModerationRequest req)
        {
            if (req == null || req.Id <= 0 || (req.Type != TblContentReport.TypePost && req.Type != TblContentReport.TypeComment))
                return Json(new { success = false, message = T("Yêu cầu không hợp lệ.", "Invalid request.") });

            await MarkReports(req.Type, new[] { req.Id }, TblContentReport.StatusDismissed);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = T("Đã bỏ qua các báo cáo.", "Reports dismissed.") });
        }
    }
}

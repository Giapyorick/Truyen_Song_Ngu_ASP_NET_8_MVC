using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using WebTruyenTranh.Hubs;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Controllers
{
    public class CommunityController : Controller
    {
        private readonly TruyenSongNguContext _context;
        private readonly IHubContext<CommunityHub> _hubContext;

        public CommunityController(TruyenSongNguContext context, IHubContext<CommunityHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private int GetCurrentUserId()
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claimId, out int parsedId)) return parsedId;
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private bool IsVietnamese()
        {
            var cultureCookie = Request.Cookies[".AspNetCore.Culture"];
            if (!string.IsNullOrEmpty(cultureCookie) && cultureCookie.Contains("en")) return false;
            return CultureInfo.CurrentUICulture.Name.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
        }

        // 1. INDEX (Include Story và Chapter cho cả Comments)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int currentUserId = GetCurrentUserId();

            var posts = await _context.TblPosts
                .Include(p => p.User)
                .Include(p => p.Story)
                .Include(p => p.Chapter)
                .Include(p => p.PostLikes)
                .Include(p => p.PostComments)
                    .ThenInclude(c => c.User)
                .Include(p => p.PostComments)
                    .ThenInclude(c => c.Story)
                .Include(p => p.PostComments)
                    .ThenInclude(c => c.Chapter)
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.CurrentUserId = currentUserId;
            ViewBag.Stories = await _context.TblStories.AsNoTracking().OrderBy(s => s.Title).ToListAsync();

            return View(posts);
        }

        // 2. TẠO BÀI VIẾT
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] PostCreatedViewModel req)
        {
            bool isVi = IsVietnamese();
            int userId = GetCurrentUserId();
            if (userId == 0) return Json(new { success = false, requireLogin = true, message = isVi ? "Vui lòng đăng nhập!" : "Please sign in!" });

            if (req == null || string.IsNullOrWhiteSpace(req.Content))
                return Json(new { success = false, message = isVi ? "Nội dung bài viết không được để trống!" : "Content cannot be empty!" });

            TblStory? story = req.StoryId > 0 ? await _context.TblStories.FirstOrDefaultAsync(x => x.StoryId == req.StoryId) : null;
            TblChapter? chapter = req.ChapterId > 0 ? await _context.TblChapters.FirstOrDefaultAsync(x => x.ChapterId == req.ChapterId) : null;
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.UserId == userId);
            if (user == null) return Json(new { success = false, message = "Tài khoản không tồn tại!" });

            var post = new TblPost
            {
                UserId = userId,
                Content = req.Content.Trim(),
                StoryId = req.StoryId > 0 ? req.StoryId : null,
                ChapterId = req.ChapterId > 0 ? req.ChapterId : null,
                CreatedAt = DateTime.Now,
                IsSpoiler = req.IsSpoiler
            };

            _context.TblPosts.Add(post);
            await _context.SaveChangesAsync();

            req.PostId = post.PostId;
            req.UserId = userId;
            req.UserName = user.Name ?? (isVi ? "Ẩn danh" : "Anonymous");
            req.UserImage = user.Img;
            req.StoryTitle = story?.Title;
            req.ChapterTitle = chapter != null ? $"Chương {chapter.ChapterNumber}" : null;
            req.CreatedAt = post.CreatedAt.ToString("dd/MM/yyyy HH:mm");


            await _hubContext.Clients.All.SendAsync("PostCreated", req);
            return Json(new { success = true, message = isVi ? "Đăng bài thành công!" : "Published successfully!" });
        }
        [HttpGet]
        public async Task<IActionResult> GetPosts(int page = 1, int pageSize = 10, string filterTab = "latest", string search = "", int? filterStoryId = null)
        {
            int currentUserId = GetCurrentUserId();

            var query = _context.TblPosts
                .Include(p => p.User)
                .Include(p => p.Story)
                .Include(p => p.Chapter)
                .Include(p => p.PostLikes)
                .Include(p => p.PostComments)
                    .ThenInclude(c => c.User)
                .AsNoTracking()
                .AsQueryable();

            // 1. Lọc theo từ khóa / Hashtag
            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim().ToLower();
                query = query.Where(p => p.Content.ToLower().Contains(keyword) ||
                                         (p.User != null && p.User.Name.ToLower().Contains(keyword)));
            }

            // 2. Lọc theo Truyện đính kèm
            if (filterStoryId.HasValue && filterStoryId.Value > 0)
            {
                query = query.Where(p => p.StoryId == filterStoryId.Value);
            }

            // 3. Xử lý Tab Sắp xếp & Lọc
            switch (filterTab.ToLower())
            {
                case "my_posts": // Bài viết của tôi
                    query = query.Where(p => p.UserId == currentUserId);
                    query = query.OrderByDescending(p => p.CreatedAt);
                    break;

                case "popular": // Nổi bật (Nhiều Tim + Bình luận nhất)
                    query = query.OrderByDescending(p => (p.PostLikes.Count * 2) + p.PostComments.Count)
                                 .ThenByDescending(p => p.CreatedAt);
                    break;

                case "latest": // Mới nhất
                default:
                    query = query.OrderByDescending(p => p.CreatedAt);
                    break;
            }

            var posts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    postId = p.PostId,
                    userId = p.UserId,
                    userName = p.User != null ? p.User.Name : "Ẩn danh",
                    userImage = p.User != null ? p.User.Img : null,
                    content = p.Content,
                    createdAt = p.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    isSpoiler = p.IsSpoiler,
                    storyId = p.StoryId,
                    storyTitle = p.Story != null ? p.Story.Title : null,
                    chapterId = p.ChapterId,
                    chapterTitle = p.Chapter != null ? $"Chương {p.Chapter.ChapterNumber}" : null,
                    totalLikes = p.PostLikes != null ? p.PostLikes.Count : 0,
                    isLikedByMe = p.PostLikes != null && p.PostLikes.Any(l => l.UserId == currentUserId),
                    totalComments = p.PostComments != null ? p.PostComments.Count : 0,
                    comments = p.PostComments != null
                        ? p.PostComments.Select(c => new {
                            commentId = c.CommentId,
                            postId = c.PostId,
                            userId = c.UserId,
                            userName = c.User != null ? c.User.Name : "Ẩn danh",
                            userImage = c.User != null ? c.User.Img : null,
                            content = c.Content,
                            isSpoiler = c.IsSpoiler,
                            parentCommentId = c.ParentCommentId,
                            storyId = c.StoryId,
                            storyTitle = c.Story != null ? c.Story.Title : null,
                            chapterId = c.ChapterId,
                            chapterTitle = c.Chapter != null ? $"Chương {c.Chapter.ChapterNumber}" : null,
                            createdAt = c.CreatedAt.ToString("HH:mm dd/MM")
                        }).ToList()
                        : null
                })
                .ToListAsync();

            return Json(posts);
        }

        // 3. XÓA BÀI VIẾT (Cascade Delete)
        [HttpPost]
        public async Task<IActionResult> DeletePost(int postId)
        {
            bool isVi = IsVietnamese();
            int userId = GetCurrentUserId();
            if (userId == 0) return Json(new { success = false, message = isVi ? "Vui lòng đăng nhập!" : "Please sign in!" });

            var post = await _context.TblPosts
                .Include(p => p.PostLikes)
                .Include(p => p.PostComments)
                .FirstOrDefaultAsync(p => p.PostId == postId);

            if (post == null) return Json(new { success = false, message = isVi ? "Bài viết không tồn tại!" : "Post not found!" });

            // Cho phép tác giả hoặc Admin xóa
            if (post.UserId != userId) return Json(new { success = false, message = isVi ? "Bạn không có quyền xóa bài viết này!" : "Unauthorized!" });

            if (post.PostLikes != null) _context.TblPostLikes.RemoveRange(post.PostLikes);
            if (post.PostComments != null) _context.TblPostComments.RemoveRange(post.PostComments);
            _context.TblPosts.Remove(post);

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("PostDeleted", postId);

            return Json(new { success = true });
        }

        // 4. THẢ TIM BÀI VIẾT
        [HttpPost]
        public async Task<IActionResult> ToggleLike([FromBody] LikeUpdatedViewModel req)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Json(new { success = false, requireLogin = true });

            var existingLike = await _context.TblPostLikes.FirstOrDefaultAsync(x => x.PostId == req.PostId && x.UserId == userId);
            bool isLiked;

            if (existingLike != null)
            {
                _context.TblPostLikes.Remove(existingLike);
                isLiked = false;
            }
            else
            {
                _context.TblPostLikes.Add(new TblPostLike { PostId = req.PostId, UserId = userId, CreatedAt = DateTime.Now });
                isLiked = true;
            }

            await _context.SaveChangesAsync();
            int totalLikes = await _context.TblPostLikes.CountAsync(x => x.PostId == req.PostId);

            req.UserId = userId;
            req.IsLiked = isLiked;
            req.TotalLikes = totalLikes;

            await _hubContext.Clients.All.SendAsync("PostLikeUpdated", req);
            return Json(new { success = true, isLiked, totalLikes });
        }

        // 5. THÊM BÌNH LUẬN (Lưu StoryId và ChapterId vào DB)
        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] CommentAddedViewModel req)
        {
            bool isVi = IsVietnamese();
            int userId = GetCurrentUserId();
            if (userId == 0) return Json(new { success = false, requireLogin = true });

            if (req == null || string.IsNullOrWhiteSpace(req.Content))
                return Json(new { success = false, message = isVi ? "Nội dung bình luận không được rỗng!" : "Empty comment!" });

            int? parentId = null;
            string? replyToUserName = null;

            if (req.ParentCommentId.HasValue && req.ParentCommentId.Value > 0)
            {
                var parent = await _context.TblPostComments.Include(c => c.User).FirstOrDefaultAsync(x => x.CommentId == req.ParentCommentId.Value);
                if (parent != null && parent.PostId == req.PostId)
                {
                    parentId = req.ParentCommentId.Value;
                    replyToUserName = parent.User?.Name ?? "Ẩn danh";
                }
            }

            TblStory? story = req.StoryId > 0 ? await _context.TblStories.FirstOrDefaultAsync(x => x.StoryId == req.StoryId) : null;
            TblChapter? chapter = req.ChapterId > 0 ? await _context.TblChapters.FirstOrDefaultAsync(x => x.ChapterId == req.ChapterId) : null;
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.UserId == userId);
            if (user == null) return Json(new { success = false, message = "Tài khoản không tồn tại!" });

            // Lưu StoryId và ChapterId vào DB
            var comment = new TblPostComment
            {
                PostId = req.PostId,
                UserId = userId,
                Content = req.Content.Trim(),
                ParentCommentId = parentId,
                StoryId = req.StoryId > 0 ? req.StoryId : null,
                ChapterId = req.ChapterId > 0 ? req.ChapterId : null,
                CreatedAt = DateTime.Now,
                IsSpoiler = req.IsSpoiler
            };

            _context.TblPostComments.Add(comment);
            await _context.SaveChangesAsync();

            int totalComments = await _context.TblPostComments.CountAsync(x => x.PostId == req.PostId);

            req.CommentId = comment.CommentId;
            req.UserId = userId;
            req.UserName = user.Name ?? (isVi ? "Ẩn danh" : "Anonymous");
            req.UserImage = user.Img;
            req.StoryTitle = story?.Title;
            req.ChapterTitle = chapter != null ? $"Chương {chapter.ChapterNumber}" : null;
            req.CreatedAt = comment.CreatedAt.ToString("HH:mm dd/MM");

            await _hubContext.Clients.All.SendAsync("CommentAdded", new { data = req, replyToUser = replyToUserName });
            return Json(new { success = true, totalComments, commentId = comment.CommentId });
        }

        // 6. XÓA BÌNH LUẬN (Tự động xóa sạch các comment con đệ quy)
        [HttpPost]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            bool isVi = IsVietnamese();
            int userId = GetCurrentUserId();
            if (userId == 0) return Json(new { success = false, message = isVi ? "Vui lòng đăng nhập!" : "Please sign in!" });

            var comment = await _context.TblPostComments.FirstOrDefaultAsync(c => c.CommentId == commentId);
            if (comment == null) return Json(new { success = false, message = isVi ? "Bình luận không tồn tại!" : "Comment not found!" });

            if (comment.UserId != userId) return Json(new { success = false, message = isVi ? "Bạn không có quyền xóa!" : "Unauthorized!" });

            int postId = comment.PostId;

            // Thu thập tất cả comment con cháu thuộc nhánh này
            List<int> idsToDelete = new List<int> { commentId };
            GetChildCommentIds(commentId, idsToDelete);

            var commentsToDelete = await _context.TblPostComments.Where(c => idsToDelete.Contains(c.CommentId)).ToListAsync();
            _context.TblPostComments.RemoveRange(commentsToDelete);
            await _context.SaveChangesAsync();

            int totalComments = await _context.TblPostComments.CountAsync(x => x.PostId == postId);

            await _hubContext.Clients.All.SendAsync("CommentDeleted", new { postId, deletedCommentIds = idsToDelete, totalComments });

            return Json(new { success = true });
        }

        // Hàm hỗ trợ tìm đệ quy tất cả Comment con
        private void GetChildCommentIds(int parentId, List<int> allIds)
        {
            var children = _context.TblPostComments.Where(c => c.ParentCommentId == parentId).Select(c => c.CommentId).ToList();
            foreach (var childId in children)
            {
                allIds.Add(childId);
                GetChildCommentIds(childId, allIds);
            }
        }

        // 7. GET CHAPTERS BY STORY
        [HttpGet]
        public async Task<IActionResult> GetChaptersByStory(int storyId)
        {
            try
            {
                bool isVi = IsVietnamese();
                var chapters = await _context.TblChapters
                    .Where(x => x.StoryId == storyId)
                    .OrderBy(x => x.ChapterId)
                    .Select(x => new
                    {
                        chapterId = x.ChapterId,
                        chapterNumber = x.ChapterNumber,
                        title = x.Title,
                        displayTitle = (isVi ? $"Chương {x.ChapterNumber}" : $"Chapter {x.ChapterNumber}") + (!string.IsNullOrEmpty(x.Title) ? $" - {x.Title}" : "")
                    })
                    .ToListAsync();

                return Json(chapters);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
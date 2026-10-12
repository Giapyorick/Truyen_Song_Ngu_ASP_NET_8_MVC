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

        // 1. INDEX (ĐÃ SỬA: Nạp danh sách bài viết truyền vào View)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int currentUserId = GetCurrentUserId();
            bool isVi = IsVietnamese();

            ViewBag.CurrentUserId = currentUserId;

            // Lấy danh sách Truyện cho Dropdown chọn
            var stories = await _context.TblStories
                .AsNoTracking()
                .Select(s => new
                {
                    StoryId = s.StoryId,
                    Title = isVi
                        ? (s.TblStoryTranslations
                            .Where(t => t.LanguageCode.StartsWith("vi"))
                            .Select(t => t.Title)
                            .FirstOrDefault() ?? s.Title)
                        : s.Title
                })
                .OrderBy(s => s.Title)
                .ToListAsync();

            ViewBag.Stories = stories;

            // Nạp 10 bài viết đầu tiên cho SSR render ban đầu
            var posts = await _context.TblPosts
                .Include(p => p.User)
                .Include(p => p.Story).ThenInclude(s => s.TblStoryTranslations)
                .Include(p => p.Chapter).ThenInclude(c => c.TblChapterTranslations)
                .Include(p => p.PostLikes)
                .Include(p => p.PostComments).ThenInclude(c => c.User)
                .Include(p => p.PostComments).ThenInclude(c => c.Story).ThenInclude(s => s.TblStoryTranslations)
                .Include(p => p.PostComments).ThenInclude(c => c.Chapter).ThenInclude(ch => ch.TblChapterTranslations)
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();

            return View(posts);
        }

        // 2. TẠO BÀI VIẾT
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] PostCreatedViewModel req)
        {
            bool isVi = IsVietnamese();
            int userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false, requireLogin = true, message = isVi ? "Vui lòng đăng nhập!" : "Please sign in!" });

            if (req == null || string.IsNullOrWhiteSpace(req.Content))
                return Json(new { success = false, message = isVi ? "Nội dung bài viết không được để trống!" : "Content cannot be empty!" });

            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.UserId == userId);
            if (user == null)
                return Json(new { success = false, message = isVi ? "Tài khoản không tồn tại!" : "Account does not exist!" });

            TblStory? story = req.StoryId > 0 ? await _context.TblStories.Include(s => s.TblStoryTranslations).FirstOrDefaultAsync(x => x.StoryId == req.StoryId) : null;
            TblChapter? chapter = req.ChapterId > 0 ? await _context.TblChapters.Include(c => c.TblChapterTranslations).FirstOrDefaultAsync(x => x.ChapterId == req.ChapterId) : null;

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

            string? storyTitle = null;
            if (story != null)
            {
                storyTitle = isVi
                    ? (story.TblStoryTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))?.Title ?? story.Title)
                    : story.Title;
            }

            string? chapterTitle = null;
            if (chapter != null)
            {
                string? chapterName = isVi
                    ? (chapter.TblChapterTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))?.Title ?? chapter.Title)
                    : chapter.Title;

                chapterTitle = (isVi ? $"Chương {chapter.ChapterNumber}" : $"Chapter {chapter.ChapterNumber}") +
                               (!string.IsNullOrWhiteSpace(chapterName) ? $" - {chapterName}" : "");
            }

            req.PostId = post.PostId;
            req.UserId = userId;
            req.UserName = user.Name ?? (isVi ? "Ẩn danh" : "Anonymous");
            req.UserImage = user.Img;
            req.StoryTitle = storyTitle;
            req.ChapterTitle = chapterTitle;
            req.CreatedAt = post.CreatedAt.ToString("dd/MM/yyyy HH:mm");

            await _hubContext.Clients.All.SendAsync("PostCreated", req);
            return Json(new { success = true, message = isVi ? "Đăng bài thành công!" : "Published successfully!" });
        }

        // 3. GET POSTS (ĐÃ SỬA: Bảo vệ an toàn chống văng lỗi Null Reference LINQ)
        [HttpGet]
        public async Task<IActionResult> GetPosts(int page = 1, int pageSize = 10, string filterTab = "latest", string search = "", int? filterStoryId = null)
        {
            int currentUserId = GetCurrentUserId();
            bool isVi = IsVietnamese();

            var query = _context.TblPosts
                .Include(p => p.User)
                .Include(p => p.Story).ThenInclude(s => s.TblStoryTranslations)
                .Include(p => p.Chapter).ThenInclude(c => c.TblChapterTranslations)
                .Include(p => p.PostLikes)
                .Include(p => p.PostComments).ThenInclude(c => c.User)
                .Include(p => p.PostComments).ThenInclude(c => c.Story).ThenInclude(s => s.TblStoryTranslations)
                .Include(p => p.PostComments).ThenInclude(c => c.Chapter).ThenInclude(ch => ch.TblChapterTranslations)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim().ToLower();
                query = query.Where(p => p.Content.ToLower().Contains(keyword) ||
                                         (p.User != null && p.User.Name.ToLower().Contains(keyword)));
            }

            if (filterStoryId.HasValue && filterStoryId.Value > 0)
            {
                query = query.Where(p => p.StoryId == filterStoryId.Value);
            }

            switch (filterTab.ToLower())
            {
                case "my_posts":
                    query = query.Where(p => p.UserId == currentUserId);
                    query = query.OrderByDescending(p => p.CreatedAt);
                    break;

                case "popular":
                    query = query.OrderByDescending(p => (p.PostLikes.Count * 2) + p.PostComments.Count)
                                 .ThenByDescending(p => p.CreatedAt);
                    break;

                case "latest":
                default:
                    query = query.OrderByDescending(p => p.CreatedAt);
                    break;
            }

            var rawPosts = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var posts = rawPosts.Select(p => {
                string? storyTitle = p.Story != null
                    ? (isVi
                        ? (p.Story.TblStoryTranslations?.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))?.Title ?? p.Story.Title)
                        : p.Story.Title)
                    : null;

                string? chapterTitle = null;
                if (p.Chapter != null)
                {
                    string? cName = isVi
                        ? (p.Chapter.TblChapterTranslations?.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))?.Title ?? p.Chapter.Title)
                        : p.Chapter.Title;
                    chapterTitle = (isVi ? $"Chương {p.Chapter.ChapterNumber}" : $"Chapter {p.Chapter.ChapterNumber}") +
                                   (!string.IsNullOrEmpty(cName) ? $" - {cName}" : "");
                }

                return new
                {
                    postId = p.PostId,
                    userId = p.UserId,
                    userName = p.User != null ? p.User.Name : (isVi ? "Ẩn danh" : "Anonymous"),
                    userImage = p.User != null ? p.User.Img : null,
                    content = p.Content,
                    createdAt = p.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    isSpoiler = p.IsSpoiler,
                    storyId = p.StoryId,
                    storyTitle = storyTitle,
                    chapterId = p.ChapterId,
                    chapterTitle = chapterTitle,
                    totalLikes = p.PostLikes != null ? p.PostLikes.Count : 0,
                    isLikedByMe = p.PostLikes != null && p.PostLikes.Any(l => l.UserId == currentUserId),
                    totalComments = p.PostComments != null ? p.PostComments.Count : 0,
                    comments = p.PostComments != null
                        ? p.PostComments.Select(c => {
                            string? cStoryTitle = c.Story != null
                                ? (isVi
                                    ? (c.Story.TblStoryTranslations?.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))?.Title ?? c.Story.Title)
                                    : c.Story.Title)
                                : null;

                            string? cChapterTitle = null;
                            if (c.Chapter != null)
                            {
                                string? ccName = isVi
                                    ? (c.Chapter.TblChapterTranslations?.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))?.Title ?? c.Chapter.Title)
                                    : c.Chapter.Title;
                                cChapterTitle = (isVi ? $"Chương {c.Chapter.ChapterNumber}" : $"Chapter {c.Chapter.ChapterNumber}") +
                                                (!string.IsNullOrEmpty(ccName) ? $" - {ccName}" : "");
                            }

                            return new
                            {
                                commentId = c.CommentId,
                                postId = c.PostId,
                                userId = c.UserId,
                                userName = c.User != null ? c.User.Name : (isVi ? "Ẩn danh" : "Anonymous"),
                                userImage = c.User != null ? c.User.Img : null,
                                content = c.Content,
                                isSpoiler = c.IsSpoiler,
                                parentCommentId = c.ParentCommentId,
                                storyId = c.StoryId,
                                storyTitle = cStoryTitle,
                                chapterId = c.ChapterId,
                                chapterTitle = cChapterTitle,
                                createdAt = c.CreatedAt.ToString("HH:mm dd/MM")
                            };
                        }).ToList()
                        : null
                };
            });

            return Json(posts);
        }

        // 4. XÓA BÀI VIẾT
        [HttpPost]
        public async Task<IActionResult> DeletePost(int postId)
        {
            bool isVi = IsVietnamese();
            int userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false, message = isVi ? "Vui lòng đăng nhập!" : "Please sign in!" });

            var post = await _context.TblPosts
                .Include(p => p.PostLikes)
                .Include(p => p.PostComments)
                .FirstOrDefaultAsync(p => p.PostId == postId);

            if (post == null)
                return Json(new { success = false, message = isVi ? "Bài viết không tồn tại!" : "Post not found!" });

            if (post.UserId != userId)
                return Json(new { success = false, message = isVi ? "Bạn không có quyền xóa bài viết này!" : "Unauthorized!" });

            if (post.PostLikes != null) _context.TblPostLikes.RemoveRange(post.PostLikes);
            if (post.PostComments != null) _context.TblPostComments.RemoveRange(post.PostComments);
            _context.TblPosts.Remove(post);

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("PostDeleted", postId);

            return Json(new { success = true, message = isVi ? "Đã xóa bài viết thành công!" : "Post deleted successfully!" });
        }

        // 5. THẢ TIM BÀI VIẾT
        [HttpPost]
        public async Task<IActionResult> ToggleLike([FromBody] LikeUpdatedViewModel req)
        {
            bool isVi = IsVietnamese();
            int userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false, requireLogin = true, message = isVi ? "Vui lòng đăng nhập!" : "Please sign in!" });

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

        // 6. THÊM BÌNH LUẬN
        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] CommentAddedViewModel req)
        {
            bool isVi = IsVietnamese();
            int userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false, requireLogin = true, message = isVi ? "Vui lòng đăng nhập!" : "Please sign in!" });

            if (req == null || string.IsNullOrWhiteSpace(req.Content))
                return Json(new { success = false, message = isVi ? "Nội dung bình luận không được rỗng!" : "Comment cannot be empty!" });

            int? parentId = null;
            string? replyToUserName = null;

            if (req.ParentCommentId.HasValue && req.ParentCommentId.Value > 0)
            {
                var parent = await _context.TblPostComments.Include(c => c.User).FirstOrDefaultAsync(x => x.CommentId == req.ParentCommentId.Value);
                if (parent != null && parent.PostId == req.PostId)
                {
                    parentId = req.ParentCommentId.Value;
                    replyToUserName = parent.User?.Name ?? (isVi ? "Ẩn danh" : "Anonymous");
                }
            }

            TblStory? story = req.StoryId > 0 ? await _context.TblStories.Include(s => s.TblStoryTranslations).FirstOrDefaultAsync(x => x.StoryId == req.StoryId) : null;
            TblChapter? chapter = req.ChapterId > 0 ? await _context.TblChapters.Include(c => c.TblChapterTranslations).FirstOrDefaultAsync(x => x.ChapterId == req.ChapterId) : null;
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.UserId == userId);
            if (user == null)
                return Json(new { success = false, message = isVi ? "Tài khoản không tồn tại!" : "Account does not exist!" });

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

            string? storyTitle = null;
            if (story != null)
            {
                storyTitle = isVi
                    ? (story.TblStoryTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))?.Title ?? story.Title)
                    : story.Title;
            }

            string? chapterTitle = null;
            if (chapter != null)
            {
                string? chapterName = isVi
                    ? (chapter.TblChapterTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))?.Title ?? chapter.Title)
                    : chapter.Title;

                chapterTitle = (isVi ? $"Chương {chapter.ChapterNumber}" : $"Chapter {chapter.ChapterNumber}") +
                               (!string.IsNullOrWhiteSpace(chapterName) ? $" - {chapterName}" : "");
            }

            req.CommentId = comment.CommentId;
            req.UserId = userId;
            req.UserName = user.Name ?? (isVi ? "Ẩn danh" : "Anonymous");
            req.UserImage = user.Img;
            req.StoryTitle = storyTitle;
            req.ChapterTitle = chapterTitle;
            req.CreatedAt = comment.CreatedAt.ToString("HH:mm dd/MM");

            await _hubContext.Clients.All.SendAsync("CommentAdded", new { data = req, replyToUser = replyToUserName });
            return Json(new { success = true, totalComments, commentId = comment.CommentId });
        }

        // 7. XÓA BÌNH LUẬN
        [HttpPost]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            bool isVi = IsVietnamese();
            int userId = GetCurrentUserId();
            if (userId == 0)
                return Json(new { success = false, message = isVi ? "Vui lòng đăng nhập!" : "Please sign in!" });

            var comment = await _context.TblPostComments.FirstOrDefaultAsync(c => c.CommentId == commentId);
            if (comment == null)
                return Json(new { success = false, message = isVi ? "Bình luận không tồn tại!" : "Comment not found!" });

            if (comment.UserId != userId)
                return Json(new { success = false, message = isVi ? "Bạn không có quyền xóa bình luận này!" : "Unauthorized!" });

            int postId = comment.PostId;

            List<int> idsToDelete = new List<int> { commentId };
            GetChildCommentIds(commentId, idsToDelete);

            var commentsToDelete = await _context.TblPostComments.Where(c => idsToDelete.Contains(c.CommentId)).ToListAsync();
            _context.TblPostComments.RemoveRange(commentsToDelete);
            await _context.SaveChangesAsync();

            int totalComments = await _context.TblPostComments.CountAsync(x => x.PostId == postId);

            await _hubContext.Clients.All.SendAsync("CommentDeleted", new { postId, deletedCommentIds = idsToDelete, totalComments });

            return Json(new { success = true, message = isVi ? "Đã xóa bình luận!" : "Comment deleted!" });
        }

        private void GetChildCommentIds(int parentId, List<int> allIds)
        {
            var children = _context.TblPostComments.Where(c => c.ParentCommentId == parentId).Select(c => c.CommentId).ToList();
            foreach (var childId in children)
            {
                allIds.Add(childId);
                GetChildCommentIds(childId, allIds);
            }
        }

        // 8. GET CHAPTERS BY STORY
        [HttpGet]
        public async Task<IActionResult> GetChaptersByStory(int storyId)
        {
            try
            {
                bool isVi = IsVietnamese();
                var chapters = await _context.TblChapters
                    .Include(x => x.TblChapterTranslations)
                    .Where(x => x.StoryId == storyId)
                    .OrderBy(x => x.ChapterNumber)
                    .Select(x => new
                    {
                        chapterId = x.ChapterId,
                        chapterNumber = x.ChapterNumber,
                        title = isVi
                            ? (x.TblChapterTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))!.Title ?? x.Title)
                            : x.Title,
                        displayTitle = (isVi ? $"Chương {x.ChapterNumber}" : $"Chapter {x.ChapterNumber}") +
                                       (!string.IsNullOrEmpty(isVi ? (x.TblChapterTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))!.Title ?? x.Title) : x.Title)
                                           ? $" - {(isVi ? (x.TblChapterTranslations.FirstOrDefault(t => t.LanguageCode.StartsWith("vi"))!.Title ?? x.Title) : x.Title)}"
                                           : "")
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Components
{
    [ViewComponent(Name = "HeroSlideshow")]
    public class HeroSlideshowComponent : ViewComponent
    {
        private readonly TruyenSongNguContext _context;

        public HeroSlideshowComponent(TruyenSongNguContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            // 1. Nhận diện ngôn ngữ hiện tại
            var currentCulture = CultureInfo.CurrentUICulture.Name;
            bool isVi = currentCulture.StartsWith("vi", System.StringComparison.OrdinalIgnoreCase);
            string langPrefix = isVi ? "vi" : "en";

            // 2. Lấy 5 truyện mới nhất (ưu tiên truyện có ảnh)
            var stories = await _context.TblStories
                .AsNoTracking()
                .OrderByDescending(s => s.StoryId)
                .Take(5)
                .Select(s => new
                {
                    s.StoryId,
                    Title = isVi
                        ? (s.TblStoryTranslations
                            .Where(t => t.LanguageCode.StartsWith(langPrefix))
                            .Select(t => t.Title)
                            .FirstOrDefault() ?? s.Title ?? "Undefined")
                        : (s.Title ?? "Undefined"),
                    Description = isVi
                        ? (s.TblStoryTranslations
                            .Where(t => t.LanguageCode.StartsWith(langPrefix))
                            .Select(t => t.Description)
                            .FirstOrDefault() ?? s.Description ?? "")
                        : (s.Description ?? ""),
                    s.Img
                })
                .ToListAsync();

            var storyIds = stories.Select(s => s.StoryId).ToList();

            // 3. Kiểm tra trạng thái đã Follow của user đang đăng nhập
            var followedSet = new System.Collections.Generic.HashSet<int>();
            if (userId > 0)
            {
                var followedList = await _context.TblUserFollowStories
                    .AsNoTracking()
                    .Where(f => f.UserId == userId && storyIds.Contains(f.StoryId))
                    .Select(f => f.StoryId)
                    .ToListAsync();
                followedSet = new System.Collections.Generic.HashSet<int>(followedList);
            }

            // 4. Map ra ViewModel
            var result = stories.Select(s => new HeroBannerViewModel
            {
                StoryId = s.StoryId,
                Title = s.Title,
                Description = s.Description,
                Img = string.IsNullOrEmpty(s.Img) ? "assets/image/placeholder.png" : s.Img.TrimStart('/'),
                IsFollowed = followedSet.Contains(s.StoryId)
            }).ToList();

            return View("Default", result);
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace WebTruyenTranh.Components
{
    [ViewComponent(Name = "TopViewList")]
    public class TopViewListComponent : ViewComponent
    {
        private readonly TruyenSongNguContext _context;

        public TopViewListComponent(TruyenSongNguContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // 1. Xác định ngôn ngữ hiện tại của người dùng
            var rqCulture = HttpContext.Features.Get<IRequestCultureFeature>();
            var cultureName = rqCulture?.RequestCulture.UICulture.Name
                              ?? Request.Cookies[CookieRequestCultureProvider.DefaultCookieName]
                              ?? CultureInfo.CurrentUICulture.Name;

            bool isVi = cultureName.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

            // 2. Truy vấn top 10 truyện theo lượt Likes và lấy đúng tiêu đề song ngữ
            var topStories = await _context.TblStories
                .AsNoTracking()
                .OrderByDescending(s => s.Likes)
                .Take(10)
                .Select(s => new TopViewListViewModel
                {
                    StoryId = s.StoryId,
                    // Nếu là Tiếng Việt: ưu tiên lấy từ TblStoryTranslations (vi-VN), nếu chưa dịch thì fallback về s.Title
                    // Nếu là Tiếng Anh: lấy trực tiếp từ s.Title gốc
                    Title = isVi
                        ? (s.TblStoryTranslations
                            .Where(t => t.LanguageCode.StartsWith("vi"))
                            .Select(t => t.Title)
                            .FirstOrDefault() ?? s.Title ?? "Untitled")
                        : (s.Title ?? "Untitled"),
                    Likes = s.Likes,
                    img = s.Img ?? "assets/image/placeholder.png",
                    author = s.Author != null ? (s.Author.AuthorName ?? "Unknown") : "Unknown"
                })
                .ToListAsync();

            return View("TopViewList", topStories);
        }
    }
}
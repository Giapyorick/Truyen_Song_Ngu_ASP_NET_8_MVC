using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Components
{
    [ViewComponent(Name = "ChaptersList")]
    public class ChaptersListComponent : ViewComponent
    {
        private readonly TruyenSongNguContext _context;

        public ChaptersListComponent(TruyenSongNguContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(int storyId, int page = 1)
        {
            // 1. Xác định tiền tố ngôn ngữ hiện tại (vi hoặc en)
            var currentCulture = CultureInfo.CurrentUICulture.Name;
            bool isVi = currentCulture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
            string langPrefix = isVi ? "vi" : "en";

            int pageSize = 10;
            if (page < 1) page = 1;

            int totalChapters = await _context.TblChapters.CountAsync(c => c.StoryId == storyId);
            int totalPages = (int)Math.Ceiling((double)totalChapters / pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            // 2. Nạp chương kèm tiêu đề song ngữ từ TblChapterTranslations
            var chapters = await _context.TblChapters
                .AsNoTracking()
                .Where(c => c.StoryId == storyId)
                .OrderBy(c => c.ChapterNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ChaptersListViewModel
                {
                    ChapterId = c.ChapterId,
                    ChapterNumber = c.ChapterNumber,
                    // Ưu tiên lấy tiêu đề theo ngôn ngữ đang chọn, nếu không có mới lấy c.Title gốc
                    Title = c.TblChapterTranslations
                             .Where(t => t.LanguageCode.StartsWith(langPrefix))
                             .Select(t => t.Title)
                             .FirstOrDefault() ?? c.Title,
                    CreatedDate = c.CreatedDate
                })
                .ToListAsync();

            var model = new ChaptersPagedViewModel
            {
                Chapters = chapters,
                CurrentPage = page,
                TotalPages = totalPages,
                StoryId = storyId
            };

            return View("ChaptersList", model);
        }
    }
}
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Helpers
{
    /// <summary>
    /// Gắn tiến độ đọc của người dùng (đang đọc chương mấy) vào danh sách thẻ truyện,
    /// để huy hiệu "Chương X" trên _StoryCardsPartial hiện đúng ở mọi trang
    /// (trang chủ, thể loại, xếp hạng, tìm kiếm) – giống trang Hồ sơ.
    /// Chỉ chạy 2 truy vấn nhỏ cho cả danh sách, không truy vấn theo từng truyện.
    /// </summary>
    public static class ReadingProgressHelper
    {
        public static async Task FillAsync(TruyenSongNguContext context, IList<StoryListViewModel>? stories, int userId)
        {
            if (stories == null || stories.Count == 0 || userId <= 0) return;

            var storyIds = stories.Select(s => s.StoryID).Distinct().ToList();

            // 1. Chương đọc gần nhất của user cho các truyện đang hiển thị
            var progress = await context.TblUserReadingProgresses
                .AsNoTracking()
                .Where(p => p.UserId == userId && storyIds.Contains(p.StoryId) && p.LastChapterId > 0)
                .Select(p => new { p.StoryId, p.LastChapterId })
                .ToListAsync();

            if (progress.Count == 0) return;

            // 2. Số thứ tự của các chương đó (để hiện "Chương 3")
            var chapterIds = progress.Select(p => p.LastChapterId).Distinct().ToList();
            var chapterNumbers = await context.TblChapters
                .AsNoTracking()
                .Where(c => chapterIds.Contains(c.ChapterId))
                .Select(c => new { c.ChapterId, c.ChapterNumber })
                .ToDictionaryAsync(c => c.ChapterId, c => c.ChapterNumber);

            var byStory = progress
                .GroupBy(p => p.StoryId)
                .ToDictionary(g => g.Key, g => g.First().LastChapterId);

            foreach (var s in stories)
            {
                if (!byStory.TryGetValue(s.StoryID, out var chapterId)) continue;
                // Chương đã bị xóa thì coi như chưa đọc
                if (!chapterNumbers.TryGetValue(chapterId, out var number)) continue;

                s.HasProgress = true;
                s.LastChapterId = chapterId;
                s.LastChapterNumber = number;
            }
        }
    }
}

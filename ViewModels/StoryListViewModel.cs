using System;
using System.Collections.Generic;

namespace WebTruyenTranh.ViewModels
{
    public class LatestChapterItemViewModel
    {
        public int ChapterId { get; set; }
        public int ChapterNumber { get; set; }
        public string? ChapterTitle { get; set; }
    }

    public class StoryListViewModel
    {
        public int StoryID { get; set; }
        /// <summary>Mã định danh YYMMNNN (hiển thị kèm dấu #), có thể null nếu chưa chạy StoryCode.sql</summary>
        public string? StoryCode { get; set; }
        public string Title { get; set; } = null!;
        public DateOnly? PublicationDate { get; set; }
        public string? Img { get; set; }
        public int? Likes { get; set; }
        public string? Description { get; set; }
        public double? Rate { get; set; }
        public int? CountFolower { get; set; }
        public int? CountRate { get; set; }
        public string? Status { get; set; }
        public string? Lang { get; set; }
        public bool HasProgress { get; set; }
        public int? LastChapterId { get; set; }
        public int? LastChapterNumber { get; set; }
        public List<string>? Categories { get; set; }

        public List<LatestChapterItemViewModel> LatestChapters { get; set; } = new();
    }
}
namespace WebTruyenTranh.ViewModels
{
    /// <summary>Thể loại nổi bật hiển thị trên trang chủ (lọc truyện)</summary>
    public class FeaturedCategoryViewModel
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = "";
        public int StoryCount { get; set; }
    }
}

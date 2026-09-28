using System.Collections.Generic;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.ViewModels
{
    public class CategoryStoriesViewModel
    {
        // Danh sách tất cả thể loại để hiển thị menu/thẻ lọc
        public List<CategoryItemDto> Categories { get; set; } = new();

        // Thể loại hiện tại đang chọn (nếu có)
        public int? SelectedCategoryId { get; set; }
        public string? SelectedCategoryName { get; set; }
        public string? SelectedCategoryDesc { get; set; }

        // Danh sách truyện thuộc thể loại đang chọn
        public List<StoryItemDto> Stories { get; set; } = new();
    }

    public class CategoryItemDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int StoryCount { get; set; }
    }

    public class StoryItemDto
    {
        public int StoryId { get; set; }
        public string Title { get; set; } = "";
        public string? Image { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string? Lang { get; set; }
        public int TotalChapters { get; set; }
        public List<string> Categories { get; set; } = new();
    }
}
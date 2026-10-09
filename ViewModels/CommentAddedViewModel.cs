namespace WebTruyenTranh.ViewModels
{
    public class CommentAddedViewModel
    {
        public int CommentId { get; set; }

        public int PostId { get; set; }

        public int UserId { get; set; }

        public string UserName { get; set; } = "Ẩn danh";

        public string? UserImage { get; set; }

        public string Content { get; set; } = string.Empty;

        public int? ParentCommentId { get; set; }

        // Bổ sung các thuộc tính đính kèm Truyện & Chương
        public int? StoryId { get; set; }

        public string? StoryTitle { get; set; }

        public int? ChapterId { get; set; }

        public string? ChapterTitle { get; set; }

        public string CreatedAt { get; set; } = string.Empty;
        public bool IsSpoiler { get; set; } = false;

    }
}
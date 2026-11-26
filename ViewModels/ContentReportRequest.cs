namespace WebTruyenTranh.ViewModels
{
    /// <summary>Dữ liệu gửi lên khi người đọc báo cáo một bài viết / bình luận (CommunityController.Report)</summary>
    public class ContentReportRequest
    {
        /// <summary>"post" hoặc "comment"</summary>
        public string TargetType { get; set; } = "";
        public int TargetId { get; set; }
        /// <summary>spam | offensive | spoiler | inappropriate | other</summary>
        public string? Reason { get; set; }
        public string? Note { get; set; }
    }
}

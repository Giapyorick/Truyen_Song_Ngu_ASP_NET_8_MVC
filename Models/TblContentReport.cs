using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebTruyenTranh.Models
{
    /// <summary>
    /// Báo cáo vi phạm do người đọc gửi cho một bài viết hoặc bình luận ở trang Cộng đồng.
    /// Bảng tạo bởi StoreProcedure/CommunityModeration.sql
    /// </summary>
    [Table("tblContentReports")]
    public class TblContentReport
    {
        public const string TypePost = "post";
        public const string TypeComment = "comment";

        public const string StatusPending = "pending";     // chờ xử lý
        public const string StatusResolved = "resolved";   // đã xử lý (ẩn / xóa nội dung)
        public const string StatusDismissed = "dismissed"; // bỏ qua (nội dung không vi phạm)

        [Key]
        public int ReportId { get; set; }

        /// <summary>"post" hoặc "comment"</summary>
        [Required, StringLength(10)]
        public string TargetType { get; set; } = TypePost;

        /// <summary>PostId hoặc CommentId tùy TargetType</summary>
        public int TargetId { get; set; }

        public int ReporterUserId { get; set; }

        /// <summary>spam | offensive | spoiler | inappropriate | other</summary>
        [Required, StringLength(30)]
        public string Reason { get; set; } = "other";

        [StringLength(300)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required, StringLength(20)]
        public string Status { get; set; } = StatusPending;

        public DateTime? ResolvedAt { get; set; }

        public int? ResolvedByAdminId { get; set; }

        [ForeignKey("ReporterUserId")]
        public virtual TblUser? Reporter { get; set; }
    }
}

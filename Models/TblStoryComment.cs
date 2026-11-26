using System;

namespace WebTruyenTranh.Models;

/// <summary>
/// Bình luận truyện ở trang chi tiết (bảng dbo.tblStoryComment – script StoreProcedure/StoryComment.sql).
/// Bảng riêng, không dùng tblComment cũ (bảng cũ trong CSDL thật không khớp model).
/// </summary>
public partial class TblStoryComment
{
    public int StoryCommentId { get; set; }

    public int StoryId { get; set; }

    /// <summary>null khi tài khoản đã bị xóa (bình luận vẫn giữ lại)</summary>
    public int? UserId { get; set; }

    /// <summary>Tên người bình luận tại thời điểm gửi</summary>
    public string? UserName { get; set; }

    public string Contents { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

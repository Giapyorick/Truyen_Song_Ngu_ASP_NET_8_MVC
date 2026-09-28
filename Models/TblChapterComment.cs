using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblChapterComment
{
    public int CommentId { get; set; }

    public int UserId { get; set; }

    public int ChapterId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime? CreateAt { get; set; }

    public bool? Status { get; set; }

    public virtual TblChapter Chapter { get; set; } = null!;

    public virtual TblUser User { get; set; } = null!;
}

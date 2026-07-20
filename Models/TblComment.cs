using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblComment
{
    public int CommentId { get; set; }

    public int StoryId { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    public string Contents { get; set; } = null!;

    public DateTime? CreateAd { get; set; }

    public int? CountLikes { get; set; }

    public virtual TblStory Story { get; set; } = null!;
}

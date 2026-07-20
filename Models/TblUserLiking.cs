using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblUserLiking
{
    public int UserId { get; set; }

    public int StoryId { get; set; }

    public int Liking { get; set; }

    public DateTime? LikedDate { get; set; }

    public virtual TblStory Story { get; set; } = null!;

    public virtual TblUser User { get; set; } = null!;
}

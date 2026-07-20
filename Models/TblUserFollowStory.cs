using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblUserFollowStory
{
    public int FollowId { get; set; }

    public int UserId { get; set; }

    public int StoryId { get; set; }

    public virtual TblStory Story { get; set; } = null!;

    public virtual TblUser User { get; set; } = null!;
}

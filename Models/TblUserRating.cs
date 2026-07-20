using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblUserRating
{
    public int UserId { get; set; }

    public int StoryId { get; set; }

    public int Rating { get; set; }

    public DateTime? RatedDate { get; set; }

    public virtual TblStory Story { get; set; } = null!;

    public virtual TblUser User { get; set; } = null!;
}

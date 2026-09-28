using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblCategoryOfStory
{
    public int CategoryOfStoryId { get; set; }

    public int CategoryId { get; set; }

    public int StoryId { get; set; }

    public virtual TblCategory Category { get; set; } = null!;

    public virtual TblStory Story { get; set; } = null!;
}

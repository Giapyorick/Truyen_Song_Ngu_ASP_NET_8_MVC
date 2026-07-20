using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblUserReadingProgress
{
    public int UserId { get; set; }

    public int StoryId { get; set; }

    public int LastChapterId { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

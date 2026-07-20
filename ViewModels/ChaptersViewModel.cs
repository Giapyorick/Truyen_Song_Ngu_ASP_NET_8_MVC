using System;
using System.Collections.Generic;

namespace WebTruyenTranh.ViewModels;

public partial class ChaptersViewModel
{
    public int ChapterId { get; set; }

    public int StoryId { get; set; }

    public int ChapterNumber { get; set; }

    public string? Title { get; set; }

    public DateTime? CreatedDate { get; set; }
}

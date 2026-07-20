using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblChapter
{
    public int ChapterId { get; set; }

    public int StoryId { get; set; }

    public int ChapterNumber { get; set; }

    public string? Title { get; set; }

    public DateTime? CreatedDate { get; set; }

    public virtual TblStory Story { get; set; } = null!;

    public virtual ICollection<TblChapterComment> TblChapterComments { get; set; } = new List<TblChapterComment>();

    public virtual ICollection<TblParagraph> TblParagraphs { get; set; } = new List<TblParagraph>();
}

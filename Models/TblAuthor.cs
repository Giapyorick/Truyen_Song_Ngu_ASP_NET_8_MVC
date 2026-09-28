using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblAuthor
{
    public int AuthorId { get; set; }

    public string AuthorName { get; set; } = null!;

    public DateOnly? DoB { get; set; }

    public string? Gender { get; set; }

    public string? Country { get; set; }

    public string? Email { get; set; }

    public string? Status { get; set; }

    public string? Img { get; set; }

    public virtual ICollection<TblStory> TblStories { get; set; } = new List<TblStory>();
}

using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblCategory
{
    public int CategoryId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<TblCategoryOfStory> TblCategoryOfStories { get; set; } = new List<TblCategoryOfStory>();
}

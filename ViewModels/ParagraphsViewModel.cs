using System;
using System.Collections.Generic;

namespace WebTruyenTranh.ViewModels;

public partial class ParagraphsViewModel
{
    public int ParagraphId { get; set; }

    public int ChapterId { get; set; }

    public int ParagraphOrder { get; set; }

    public string English { get; set; } = null!;

    public string Vietnamese { get; set; } = null!;
    public string? French { get; set; } = null!;

    public string? Chinese { get; set; } = null!;
    public string? Japanese { get; set; } = null!;
    public int? BlockType { get; set; } = 1;


}

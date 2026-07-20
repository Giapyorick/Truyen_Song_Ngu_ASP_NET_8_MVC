using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebTruyenTranh.ViewModels;

public partial class StoriesViewModel
{
    public int StoryId { get; set; }

    public string? Title { get; set; }

    public int? AuthorId { get; set; }

    public DateOnly? PublicationDate { get; set; }

    public string? Img { get; set; }

    public int? Likes { get; set; }

    public string? Description { get; set; }

    public double? Rate { get; set; }

    public int? CountFolower { get; set; }

    public int? CountRate { get; set; }

    public string? Status { get; set; }
	[NotMapped]
	public IFormFile? formFile {get; set;}
	public List<int> CategoryIds { get; set; } = new();

}

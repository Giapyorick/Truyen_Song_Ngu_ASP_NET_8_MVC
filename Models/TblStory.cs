using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblStory
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

    public virtual TblAuthor? Author { get; set; }

    public virtual ICollection<TblCategoryOfStory> TblCategoryOfStories { get; set; } = new List<TblCategoryOfStory>();

    public virtual ICollection<TblChapter> TblChapters { get; set; } = new List<TblChapter>();

    public virtual ICollection<TblComment> TblComments { get; set; } = new List<TblComment>();

    public virtual ICollection<TblUserFollowStory> TblUserFollowStories { get; set; } = new List<TblUserFollowStory>();

    public virtual ICollection<TblUserLiking> TblUserLikings { get; set; } = new List<TblUserLiking>();

    public virtual ICollection<TblUserRating> TblUserRatings { get; set; } = new List<TblUserRating>();
}

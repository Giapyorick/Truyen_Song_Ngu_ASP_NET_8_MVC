using System;
using System.Collections.Generic;

namespace WebTruyenTranh.Models;

public partial class TblUser
{
    public int UserId { get; set; }

    public string? Name { get; set; }

    public DateOnly? DoB { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Img { get; set; }

    public string? Gender { get; set; }

    public DateTime? CreateAd { get; set; }

    public string? Status { get; set; }

    public string? Passwork { get; set; }

    public string? ResetToken { get; set; }

    public DateTime? TokenExpiry { get; set; }

    public virtual ICollection<TblChapterComment> TblChapterComments { get; set; } = new List<TblChapterComment>();

    public virtual ICollection<TblUserFollowStory> TblUserFollowStories { get; set; } = new List<TblUserFollowStory>();

    public virtual ICollection<TblUserLiking> TblUserLikings { get; set; } = new List<TblUserLiking>();

    public virtual ICollection<TblUserRating> TblUserRatings { get; set; } = new List<TblUserRating>();
}

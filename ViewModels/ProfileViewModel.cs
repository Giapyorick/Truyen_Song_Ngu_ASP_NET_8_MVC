using System;
using System.Collections.Generic;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.ViewModels
{
    public class ProfileViewModel
    {
        public TblUser User { get; set; } = null!;
        public IList<StoryListViewModel> FollowedStories { get; set; } = new List<StoryListViewModel>();
        public IList<StoryListViewModel> LikedStories { get; set; } = new List<StoryListViewModel>();
        public IList<StoryListViewModel> ReadStories { get; set; } = new List<StoryListViewModel>(); // Bổ sung truyện đã đọc
    }
}
using System;
using System.Collections.Generic;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.ViewModels
{
    public class UserVocabItemViewModel
    {
        public int VocabId { get; set; }
        public string WordOrPhrase { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public string? ContextSentence { get; set; }
        public string? StoryTitle { get; set; }
        public int? ChapterNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class ProfileViewModel
    {
        public TblUser User { get; set; } = null!;
        public IList<StoryListViewModel> FollowedStories { get; set; } = new List<StoryListViewModel>();
        public IList<StoryListViewModel> LikedStories { get; set; } = new List<StoryListViewModel>();
        public IList<StoryListViewModel> ReadStories { get; set; } = new List<StoryListViewModel>(); // Bổ sung truyện đã đọc
        public List<UserVocabItemViewModel> Vocabularies { get; set; } = new();
    }
}
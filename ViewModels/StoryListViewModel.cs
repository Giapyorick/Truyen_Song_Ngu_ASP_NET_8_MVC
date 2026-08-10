using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebTruyenTranh.ViewModels
{
    public class StoryListViewModel
    {
        public int StoryID { get; set; }
        public string Title { get; set; } = null!;
        public DateOnly? PublicationDate { get; set; }

        public string? Img { get; set; }

        public int? Likes { get; set; }

        public string? Description { get; set; }

        public double? Rate { get; set; }

        public int? CountFolower { get; set; }

        public int? CountRate { get; set; }

        public string? Status { get; set; }
        public bool HasProgress { get; set; }
        public int? LastChapterId { get; set; }
        public int? LastChapterNumber { get; set; }
        public List<string> Categories { get; set; }
    }

}
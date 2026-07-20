using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.ViewModels
{
    public class StoryDetailViewModel
    {
        public TblStory Story { get; set; } = null!;
        public List<TblChapter> Chapters { get; set; } = new();
        public int? LastChapterId { get; set; }
    }

}
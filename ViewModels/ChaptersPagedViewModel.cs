namespace WebTruyenTranh.ViewModels
{
    public class ChaptersPagedViewModel
    {
        public List<ChaptersListViewModel> Chapters { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int StoryId { get; set; }
    }
}
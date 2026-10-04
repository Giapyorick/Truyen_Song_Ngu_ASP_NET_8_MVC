namespace WebTruyenTranh.ViewModels
{
    public class HeroBannerViewModel
    {
        public int StoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Img { get; set; } = string.Empty;
        public bool IsFollowed { get; set; }
    }
}
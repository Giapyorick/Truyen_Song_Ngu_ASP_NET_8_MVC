namespace WebTruyenTranh.ViewModels
{
    public class TopViewListViewModel
    {
        public int StoryId { get; set; }

        public string? Title { get; set; }

        public int? Likes { get; set; } = 0;
        public string? img { get; set; } = "/images/default-image.jpg"; // Default image path
        public string? author { get; set; } = "Unknown"; // Default author name
    }
}

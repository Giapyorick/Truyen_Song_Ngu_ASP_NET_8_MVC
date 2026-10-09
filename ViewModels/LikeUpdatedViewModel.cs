namespace WebTruyenTranh.ViewModels
{
    public class LikeUpdatedViewModel
    {
        public int PostId { get; set; }

        public int UserId { get; set; }

        public bool IsLiked { get; set; }

        public int TotalLikes { get; set; }

    }
}
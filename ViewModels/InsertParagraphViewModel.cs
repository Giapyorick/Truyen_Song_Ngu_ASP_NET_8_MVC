namespace WebTruyenTranh.ViewModels
{
    public class InsertParagraphViewModel
    {
        public int ChapterId { get; set; }
        public int TargetOrder { get; set; }
        public int? BlockType { get; set; } = 1;
        public string? English { get; set; }
        public string? Vietnamese { get; set; }
        public string? Chinese { get; set; }
        public string? Japanese { get; set; }
        public string? French { get; set; }
    }
}
namespace WebTruyenTranh.Models;

public partial class TblStoryTranslation
{
    public int StoryId { get; set; }
    public string LanguageCode { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public virtual TblStory? Story { get; set; } = null!;
}
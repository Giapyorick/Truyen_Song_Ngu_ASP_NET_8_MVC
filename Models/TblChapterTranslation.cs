namespace WebTruyenTranh.Models;

public partial class TblChapterTranslation
{
    public int ChapterId { get; set; }
    public string LanguageCode { get; set; } = null!;
    public string Title { get; set; } = null!;

    public virtual TblChapter Chapter { get; set; } = null!;
}
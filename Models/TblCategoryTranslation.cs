namespace WebTruyenTranh.Models;

public partial class TblCategoryTranslation
{
    public int CategoryId { get; set; }
    public string LanguageCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public virtual TblCategory Category { get; set; } = null!;
}
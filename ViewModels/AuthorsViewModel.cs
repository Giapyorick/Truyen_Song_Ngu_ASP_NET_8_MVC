using System.ComponentModel.DataAnnotations.Schema;

namespace WebTruyenTranh.ViewModels;

public partial class AuthorsViewModel
{
    public int AuthorId { get; set; }

    public string AuthorName { get; set; } = null!;

    public DateOnly? DoB { get; set; }

    public string? Gender { get; set; }

    public string? Country { get; set; }

    public string? Email { get; set; }

    public string? Status { get; set; }

	public string? Img { get; set; }
	
	[NotMapped]
    public IFormFile? formFile { get; set; }

}

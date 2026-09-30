using System.ComponentModel.DataAnnotations;

namespace WebTruyenTranh.Areas.Admin.ViewModels
{
    public class AdminUserViewModel
    {
        public int AdminId { get; set; }

        [Required(ErrorMessage = "Please enter your account!")]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        public string? Password { get; set; }

        [StringLength(100)]
        public string? FullName { get; set; }

        [Required]
        public string Role { get; set; } = "Viewer"; // Admin hoặc Viewer

        public bool IsActive { get; set; } = true;
    }
}
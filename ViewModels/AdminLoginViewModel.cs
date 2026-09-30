using System.ComponentModel.DataAnnotations;

namespace WebTruyenTranh.ViewModels
{
    public class AdminLoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Username!")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Password!")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
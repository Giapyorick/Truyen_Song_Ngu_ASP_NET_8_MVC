using System.ComponentModel.DataAnnotations;

namespace WebTruyenTranh.ViewModels
{
    /// <summary>Dữ liệu form trang Liên hệ (Views/Home/Contact.cshtml)</summary>
    public class ContactFormViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên / Please enter your name")]
        [StringLength(80)]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập email / Please enter your email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ / Invalid email")]
        [StringLength(120)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng chọn chủ đề / Please choose a topic")]
        [StringLength(40)]
        public string Topic { get; set; } = "general";

        [Required(ErrorMessage = "Vui lòng nhập nội dung / Please enter a message")]
        [StringLength(3000, MinimumLength = 10, ErrorMessage = "Nội dung từ 10 đến 3000 ký tự / Message must be 10–3000 characters")]
        public string Message { get; set; } = "";

        /// <summary>Ô bẫy chống spam (ẩn với người dùng; bot thường điền vào)</summary>
        public string? Website { get; set; }
    }
}

namespace WebTruyenTranh.Areas.Admin.ViewModels
{
    public class AdminUserViewModel
    {
        public int AdminId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Password { get; set; }                 // Mật khẩu mới
        public string? ConfirmPassword { get; set; }          // Nhập lại mật khẩu mới
        public string? CurrentAdminPassword { get; set; }     // Mật khẩu hiện tại của người đang thao tác
        public string? FullName { get; set; }
        public string Role { get; set; } = "Viewer";
        public bool IsActive { get; set; } = true;
    }
}
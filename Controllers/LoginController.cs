using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.Services;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Controllers
{
    public class LoginController : Controller
    {
        private readonly TruyenSongNguContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public LoginController(TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CheckLogin(string email, string password)
        {
            // Nhận diện linh hoạt cả 'password' lẫn 'Passwork' từ FormData
            if (string.IsNullOrWhiteSpace(password))
            {
                password = Request.Form["Passwork"].ToString();
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, message = "Please enter both email and password!" });
            }

            string cleanEmail = email.Trim().ToLower();
            string rawPassword = password.Trim();

            // Tìm tài khoản theo Email
            var user = await _context.TblUsers
                .FirstOrDefaultAsync(x => x.Email != null && x.Email.Trim().ToLower() == cleanEmail);

            if (user == null)
            {
                return Json(new { success = false, message = "Invalid email or password!" });
            }

            // Kiểm tra trạng thái tài khoản
            if (!string.Equals(user.Status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Your account has been deactivated. Please contact support!" });
            }

            string dbPassword = (user.Passwork ?? "").Trim();
            bool isPasswordCorrect = false;

            try
            {
                // Gọi chính xác theo cấu trúc gốc của hệ thống: Verify(dbHashedPass, rawPassword)
                isPasswordCorrect = PasswordHasher.Verify(dbPassword, rawPassword);
            }
            catch
            {
                // Fallback nếu tài khoản cũ dùng Hash trực tiếp hoặc mật khẩu thuần
                isPasswordCorrect = (dbPassword == PasswordHasher.Hash(rawPassword)) || (dbPassword == rawPassword);
            }

            if (!isPasswordCorrect)
            {
                return Json(new { success = false, message = "Invalid email or password!" });
            }

            // Lưu thông tin người dùng vào Session
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.Name ?? user.Email ?? "Reader");
            HttpContext.Session.SetString("UserEmail", user.Email ?? "");
            HttpContext.Session.SetString("UserAvatar", user.Img ?? "");

            int tempUserId = HttpContext.Session.GetInt32("TempPassword_UserId") ?? 0;
            bool mustChangePassword = (tempUserId == user.UserId);

            return Json(new
            {
                success = true,
                message = "Signed in successfully!",
                userId = user.UserId,
                userName = user.Name,
                requireChangePassword = mustChangePassword // <-- Cờ báo Client mở modal đổi mật khẩu
            });
        }

        [HttpPost]
        public async Task<IActionResult> Register(UsersViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Passwork))
            {
                return Json(new { success = false, message = "Email and password are required!" });
            }

            if (model.Passwork.Trim().Length < 6)
            {
                return Json(new { success = false, message = "Password must be at least 6 characters long!" });
            }

            string cleanEmail = model.Email.Trim().ToLower();

            // Kiểm tra trùng lặp email
            bool emailExists = await _context.TblUsers
                .AnyAsync(u => u.Email != null && u.Email.Trim().ToLower() == cleanEmail);

            if (emailExists)
            {
                return Json(new { success = false, message = "This email is already registered. Please sign in!" });
            }

            // Xử lý upload ảnh đại diện nếu có
            string avatarPath = "";
            if (model.formFile != null && model.formFile.Length > 0)
            {
                string folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "assets", "image", "users");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string fileName = $"User_{Guid.NewGuid()}{Path.GetExtension(model.formFile.FileName)}";
                string fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await model.formFile.CopyToAsync(stream);
                }

                avatarPath = $"assets/image/users/{fileName}";
            }

            try
            {
                var newUser = new TblUser
                {
                    Name = string.IsNullOrWhiteSpace(model.Name) ? "Reader" : model.Name.Trim(),
                    Email = cleanEmail,
                    Phone = model.Phone?.Trim(),
                    DoB = model.DoB,
                    Gender = string.IsNullOrWhiteSpace(model.Gender) ? "Male" : model.Gender,
                    Img = avatarPath,
                    Passwork = PasswordHasher.Hash(model.Passwork.Trim()), // Băm mật khẩu theo chuẩn hệ thống
                    Status = "Active",
                    CreateAd = DateTime.Now
                };

                await _context.TblUsers.AddAsync(newUser);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Account created successfully! Please sign in."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "System error during registration: " + ex.Message
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string? email, [FromServices] IEmailSenderService emailSender)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    email = Request.Form["email"].ToString();
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    return Json(new { success = false, message = "Please enter your registered email address!" });
                }

                string cleanEmail = email.Trim().ToLower();

                // 1. Kiểm tra Email có tồn tại trong CSDL hay không
                var user = await _context.TblUsers
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.Trim().ToLower() == cleanEmail);

                if (user == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "This email address is not registered in our system!"
                    });
                }

                // 2. Kiểm tra tài khoản có bị khóa không
                if (!string.Equals(user.Status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Your account has been deactivated. Please contact support!"
                    });
                }

                // 3. Tạo mật khẩu tạm ngẫu nhiên
                string tempPassword = Guid.NewGuid().ToString("N").Substring(0, 8);
                user.Passwork = PasswordHasher.Hash(tempPassword);
                await _context.SaveChangesAsync();
                HttpContext.Session.SetInt32("TempPassword_UserId", user.UserId);

                // 4. Gửi email
                string emailSubject = "Bilingual Manga - Temporary Password Recovery";
                string emailBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 24px; line-height: 1.6; color: #334155; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 16px;'>
                <h2 style='color: #0d9488; margin-top: 0;'>Password Recovery Request</h2>
                <p>Hello <b>{user.Name ?? "Reader"}</b>,</p>
                <p>We received a request to reset your account password. Here is your temporary password to sign in:</p>
                <div style='margin: 16px 0; padding: 14px 24px; background-color: #f0fdfa; border: 1.5px dashed #0d9488; font-size: 20px; font-weight: bold; letter-spacing: 2px; color: #0f766e; display: inline-block; border-radius: 8px;'>
                    {tempPassword}
                </div>
                <p style='color: #e11d48; font-size: 13px; font-weight: 600;'>* Please sign in with this temporary password and update it immediately in Edit Profile.</p>
                <hr style='border: none; border-top: 1px solid #f1f5f9; margin: 20px 0;'>
                <p style='font-size: 12px; color: #94a3b8; margin-bottom: 0;'>Best regards,<br><b>Bilingual Manga Support Team</b></p>
            </div>";

                await emailSender.SendEmailAsync(cleanEmail, emailSubject, emailBody);


                return Json(new
                {
                    success = true,
                    message = "A temporary password has been successfully sent to your email! Please check your inbox or spam."
                });
            }
            catch (Exception ex)
            {
                // Trả lỗi chi tiết về client thay vì làm sập HTTP 500
                return Json(new
                {
                    success = false,
                    message = "Mail error: " + ex.Message
                });
            }
        }
        [HttpPost]
        public async Task<IActionResult> ChangePassword(string newPassword, string confirmPassword)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId <= 0)
            {
                return Json(new { success = false, message = "Please sign in first!" });
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Trim().Length < 6)
            {
                return Json(new { success = false, message = "New password must be at least 6 characters!" });
            }

            if (newPassword.Trim() != (confirmPassword ?? "").Trim())
            {
                return Json(new { success = false, message = "Confirm password does not match!" });
            }

            var user = await _context.TblUsers.FindAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found!" });
            }

            // Cập nhật mật khẩu mới đã băm
            user.Passwork = PasswordHasher.Hash(newPassword.Trim());
            await _context.SaveChangesAsync();

            // Xóa cờ mật khẩu tạm
            HttpContext.Session.Remove("TempPassword_UserId");

            return Json(new
            {
                success = true,
                message = "Password updated successfully!"
            });
        }
        [HttpPost]
        [HttpGet]
        public IActionResult Logout()
        {
            // 1. Xóa toàn bộ Session đăng nhập
            HttpContext.Session.Clear();

            // 2. Nếu là gọi qua AJAX thì trả về JSON để Client mở modal
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Logged out successfully!" });
            }

            // Nếu người dùng truy cập trực tiếp bằng URL thì về trang chủ kèm cờ mở modal
            return RedirectToAction("Index", "Home", new { openLogin = true });
        }
    }
}
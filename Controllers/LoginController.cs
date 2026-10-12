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
        [RateLimit("UserLogin", maxRequests: 5, timeWindowInSeconds: 60)]
        public async Task<IActionResult> CheckLogin(string email, string password, string captchaAnswer)
        {
            // 1. Kiểm tra Captcha
            if (!CaptchaHelper.ValidateCaptcha(HttpContext.Session, "LoginCaptchaResult", captchaAnswer))
            {
                return Json(new { success = false, message = "Incorrect security captcha answer!", needNewCaptcha = true });
            }

            if (string.IsNullOrWhiteSpace(password)) password = Request.Form["Passwork"].ToString();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, message = "Please enter both email and password!" });
            }

            string cleanEmail = email.Trim().ToLower();
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Email != null && x.Email.Trim().ToLower() == cleanEmail);

            if (user == null || !PasswordHasher.Verify(user.Passwork ?? "", password.Trim()))
            {
                return Json(new { success = false, message = "Invalid email or password!", needNewCaptcha = true });
            }

            if (!string.Equals(user.Status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Your account has been deactivated!" });
            }

            int tempUserId = HttpContext.Session.GetInt32("TempPassword_UserId") ?? 0;
            bool mustChangePassword = (tempUserId == user.UserId);

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.Name ?? user.Email ?? "Reader");
            await HttpContext.Session.CommitAsync();

            return Json(new
            {
                success = true,
                message = "Signed in successfully!",
                requireChangePassword = mustChangePassword
            });
        }
        [HttpPost]
        [RateLimit("SendRegisterOtp", maxRequests: 3, timeWindowInSeconds: 120)]
        public async Task<IActionResult> SendRegisterOtp(string email, string captchaAnswer, [FromServices] IEmailSenderService emailSender)
        {
            // Kiểm tra Captcha trước khi gửi OTP
            if (!CaptchaHelper.ValidateCaptcha(HttpContext.Session, "RegisterCaptchaResult", captchaAnswer))
            {
                return Json(new { success = false, message = "Incorrect security captcha answer!", needNewCaptcha = true });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Please enter your email address!" });
            }

            string cleanEmail = email.Trim().ToLower();

            // Kiểm tra xem Email đã tồn tại chưa
            bool emailExists = await _context.TblUsers.AnyAsync(u => u.Email != null && u.Email.Trim().ToLower() == cleanEmail);
            if (emailExists)
            {
                return Json(new { success = false, message = "This email is already registered. Please sign in!" });
            }

            // Tạo mã OTP 6 số ngẫu nhiên
            string otpCode = new Random().Next(100000, 999999).ToString();

            // Lưu OTP vào Session (hết hạn sau 5 phút)
            HttpContext.Session.SetString("Register_OTP", otpCode);
            HttpContext.Session.SetString("Register_OTPEmail", cleanEmail);
            HttpContext.Session.SetString("Register_OTPExpiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));
            await HttpContext.Session.CommitAsync();

            string emailSubject = "Bilingual Manga - Email Verification Code";
            string emailBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 24px; color: #334155; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 16px;'>
                <h2 style='color: #0d9488; margin-top: 0;'>Email Verification Code</h2>
                <p>Hello,</p>
                <p>Thank you for registering at <b>Bilingual Manga</b>. Your email verification OTP code is:</p>
                <div style='margin: 16px 0; padding: 14px 24px; background-color: #f0fdfa; border: 1.5px dashed #0d9488; font-size: 24px; font-weight: bold; letter-spacing: 4px; color: #0f766e; display: inline-block; border-radius: 8px;'>
                    {otpCode}
                </div>
                <p style='color: #64748b; font-size: 13px;'>* This code is valid for <b>5 minutes</b>. Please do not share this code with anyone.</p>
            </div>";

            try
            {
                await emailSender.SendEmailAsync(cleanEmail, emailSubject, emailBody);
                return Json(new { success = true, message = "Verification OTP has been sent to your Gmail address!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Mail error: " + ex.Message });
            }
        }

        [HttpPost]
        [RateLimit("UserRegister", maxRequests: 3, timeWindowInSeconds: 120)]
        public async Task<IActionResult> Register(UsersViewModel model, string otpCode)
        {
            // 1. LỚP BẪY BOT (Honeypot Trap)
            string trap = Request.Form["SystemSecurity_FakeTrap"].ToString();
            if (!string.IsNullOrEmpty(trap))
            {
                return Json(new { success = true, message = "Account created successfully!" });
            }

            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Passwork))
            {
                return Json(new { success = false, message = "Email and password are required!" });
            }

            if (model.Passwork.Trim().Length < 6)
            {
                return Json(new { success = false, message = "Password must be at least 6 characters long!" });
            }

            string cleanEmail = model.Email.Trim().ToLower();

            // 2. KIỂM TRA MÃ OTP TỪ SESSION
            string? savedOtp = HttpContext.Session.GetString("Register_OTP");
            string? savedEmail = HttpContext.Session.GetString("Register_OTPEmail");
            string? expiryStr = HttpContext.Session.GetString("Register_OTPExpiry");

            if (string.IsNullOrEmpty(otpCode) || string.IsNullOrEmpty(savedOtp) || savedEmail != cleanEmail)
            {
                return Json(new { success = false, message = "Invalid OTP request. Please request a new code!" });
            }

            if (DateTime.TryParse(expiryStr, out DateTime expiryTime) && DateTime.UtcNow > expiryTime)
            {
                return Json(new { success = false, message = "OTP has expired. Please request a new verification code!" });
            }

            if (savedOtp != otpCode.Trim())
            {
                return Json(new { success = false, message = "Incorrect OTP code. Please try again!" });
            }

            // Kiểm tra lại Email trùng lặp
            bool emailExists = await _context.TblUsers.AnyAsync(u => u.Email != null && u.Email.Trim().ToLower() == cleanEmail);
            if (emailExists)
            {
                return Json(new { success = false, message = "This email is already registered. Please sign in!" });
            }

            // Upload Avatar (nếu có)
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
                    Passwork = PasswordHasher.Hash(model.Passwork.Trim()),
                    Status = "Active",
                    CreateAd = DateTime.Now
                };

                await _context.TblUsers.AddAsync(newUser);
                await _context.SaveChangesAsync();

                // Xóa Session OTP sau khi tạo tài khoản thành công
                HttpContext.Session.Remove("Register_OTP");
                HttpContext.Session.Remove("Register_OTPEmail");
                HttpContext.Session.Remove("Register_OTPExpiry");

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

                var user = await _context.TblUsers
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.Trim().ToLower() == cleanEmail);

                if (user == null)
                {
                    return Json(new { success = false, message = "This email address is not registered in our system!" });
                }

                if (!string.Equals(user.Status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "Your account has been deactivated. Please contact support!" });
                }

                // Tạo mật khẩu tạm
                string tempPassword = Guid.NewGuid().ToString("N").Substring(0, 8);
                user.Passwork = PasswordHasher.Hash(tempPassword);
                await _context.SaveChangesAsync();

                // LƯU CỜ TẠM VÀO SESSION VÀ COMMIT NGAY LẬP TỨC
                HttpContext.Session.SetInt32("TempPassword_UserId", user.UserId);
                await HttpContext.Session.CommitAsync();

                string emailSubject = "Bilingual Manga - Temporary Password Recovery";
                string emailBody = $@"
        <div style='font-family: Arial, sans-serif; padding: 24px; line-height: 1.6; color: #334155; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 16px;'>
            <h2 style='color: #0d9488; margin-top: 0;'>Password Recovery Request</h2>
            <p>Hello <b>{user.Name ?? "Reader"}</b>,</p>
            <p>We received a request to reset your account password. Here is your temporary password to sign in:</p>
            <div style='margin: 16px 0; padding: 14px 24px; background-color: #f0fdfa; border: 1.5px dashed #0d9488; font-size: 20px; font-weight: bold; letter-spacing: 2px; color: #0f766e; display: inline-block; border-radius: 8px;'>
                {tempPassword}
            </div>
            <p style='color: #e11d48; font-size: 13px; font-weight: 600;'>* Please sign in with this temporary password and update it immediately.</p>
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
                return Json(new { success = false, message = "Mail error: " + ex.Message });
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
            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        public IActionResult GetRegisterCaptcha()
        {
            string question = CaptchaHelper.GenerateMathCaptcha(HttpContext.Session, "RegisterCaptchaResult");
            return Json(new { question });
        }

        [HttpGet]
        public IActionResult GetLoginCaptcha()
        {
            string question = CaptchaHelper.GenerateMathCaptcha(HttpContext.Session, "LoginCaptchaResult");
            return Json(new { question });
        }
    }
}
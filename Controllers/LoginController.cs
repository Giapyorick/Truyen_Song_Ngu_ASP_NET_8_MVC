using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.IO;
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
        private readonly IStringLocalizer<SharedResource> _localizer;

        public LoginController(
            TruyenSongNguContext context,
            IWebHostEnvironment webHostEnvironment,
            IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _localizer = localizer;
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
            if (!CaptchaHelper.ValidateCaptcha(HttpContext.Session, "LoginCaptchaResult", captchaAnswer))
            {
                return Json(new { success = false, message = _localizer["Auth_CaptchaIncorrect"].Value, needNewCaptcha = true });
            }

            if (string.IsNullOrWhiteSpace(password)) password = Request.Form["Passwork"].ToString();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, message = _localizer["Auth_FillEmailAndPassword"].Value });
            }

            string cleanEmail = email.Trim().ToLower();
            var user = await _context.TblUsers.FirstOrDefaultAsync(x => x.Email != null && x.Email.Trim().ToLower() == cleanEmail);

            if (user == null || !PasswordHasher.Verify(user.Passwork ?? "", password.Trim()))
            {
                return Json(new { success = false, message = _localizer["Auth_InvalidCredentials"].Value, needNewCaptcha = true });
            }

            if (!string.Equals(user.Status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = _localizer["Auth_AccountDeactivated"].Value });
            }

            int tempUserId = HttpContext.Session.GetInt32("TempPassword_UserId") ?? 0;
            bool mustChangePassword = (tempUserId == user.UserId);

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.Name ?? user.Email ?? "Reader");
            await HttpContext.Session.CommitAsync();

            return Json(new
            {
                success = true,
                message = _localizer["Auth_SignInSuccess"].Value,
                requireChangePassword = mustChangePassword
            });
        }

        [HttpPost]
        [RateLimit("SendRegisterOtp", maxRequests: 3, timeWindowInSeconds: 120)]
        public async Task<IActionResult> SendRegisterOtp(string email, string captchaAnswer, [FromServices] IEmailSenderService emailSender)
        {
            if (!CaptchaHelper.ValidateCaptcha(HttpContext.Session, "RegisterCaptchaResult", captchaAnswer))
            {
                return Json(new { success = false, message = _localizer["Auth_CaptchaIncorrect"].Value, needNewCaptcha = true });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = _localizer["Auth_EnterEmailAddress"].Value });
            }

            string cleanEmail = email.Trim().ToLower();

            bool emailExists = await _context.TblUsers.AnyAsync(u => u.Email != null && u.Email.Trim().ToLower() == cleanEmail);
            if (emailExists)
            {
                return Json(new { success = false, message = _localizer["Auth_EmailAlreadyExists"].Value });
            }

            string otpCode = new Random().Next(100000, 999999).ToString();

            HttpContext.Session.SetString("Register_OTP", otpCode);
            HttpContext.Session.SetString("Register_OTPEmail", cleanEmail);
            HttpContext.Session.SetString("Register_OTPExpiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));
            await HttpContext.Session.CommitAsync();

            string emailSubject = _localizer["Email_OtpSubject"].Value;
            string emailBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 24px; color: #334155; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 16px;'>
                <h2 style='color: #0d9488; margin-top: 0;'>{_localizer["Email_OtpTitle"].Value}</h2>
                <p>{_localizer["Email_Greeting"].Value},</p>
                <p>{_localizer["Email_OtpMessage"].Value}:</p>
                <div style='margin: 16px 0; padding: 14px 24px; background-color: #f0fdfa; border: 1.5px dashed #0d9488; font-size: 24px; font-weight: bold; letter-spacing: 4px; color: #0f766e; display: inline-block; border-radius: 8px;'>
                    {otpCode}
                </div>
                <p style='color: #64748b; font-size: 13px;'>{_localizer["Email_OtpExpiryNotice"].Value}</p>
            </div>";

            try
            {
                await emailSender.SendEmailAsync(cleanEmail, emailSubject, emailBody);
                return Json(new { success = true, message = _localizer["Auth_OtpSentSuccess"].Value });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = _localizer["Email_SendError"].Value + ": " + ex.Message });
            }
        }

        [HttpPost]
        [RateLimit("UserRegister", maxRequests: 3, timeWindowInSeconds: 120)]
        public async Task<IActionResult> Register(UsersViewModel model, string otpCode)
        {
            string trap = Request.Form["SystemSecurity_FakeTrap"].ToString();
            if (!string.IsNullOrEmpty(trap))
            {
                return Json(new { success = true, message = _localizer["Auth_RegisterSuccess"].Value });
            }

            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Passwork))
            {
                return Json(new { success = false, message = _localizer["Auth_FillRequiredFields"].Value });
            }

            if (model.Passwork.Trim().Length < 6)
            {
                return Json(new { success = false, message = _localizer["Auth_PasswordMinLength"].Value });
            }

            string cleanEmail = model.Email.Trim().ToLower();

            string? savedOtp = HttpContext.Session.GetString("Register_OTP");
            string? savedEmail = HttpContext.Session.GetString("Register_OTPEmail");
            string? expiryStr = HttpContext.Session.GetString("Register_OTPExpiry");

            if (string.IsNullOrEmpty(otpCode) || string.IsNullOrEmpty(savedOtp) || savedEmail != cleanEmail)
            {
                return Json(new { success = false, message = _localizer["Auth_InvalidOtpRequest"].Value });
            }

            if (DateTime.TryParse(expiryStr, out DateTime expiryTime) && DateTime.UtcNow > expiryTime)
            {
                return Json(new { success = false, message = _localizer["Auth_OtpExpired"].Value });
            }

            if (savedOtp != otpCode.Trim())
            {
                return Json(new { success = false, message = _localizer["Auth_IncorrectOtp"].Value });
            }

            bool emailExists = await _context.TblUsers.AnyAsync(u => u.Email != null && u.Email.Trim().ToLower() == cleanEmail);
            if (emailExists)
            {
                return Json(new { success = false, message = _localizer["Auth_EmailAlreadyExists"].Value });
            }

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

                HttpContext.Session.Remove("Register_OTP");
                HttpContext.Session.Remove("Register_OTPEmail");
                HttpContext.Session.Remove("Register_OTPExpiry");

                return Json(new
                {
                    success = true,
                    message = _localizer["Auth_RegisterSuccess"].Value
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = _localizer["Auth_SystemError"].Value + ": " + ex.Message
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
                    return Json(new { success = false, message = _localizer["Auth_EnterEmailAddress"].Value });
                }

                string cleanEmail = email.Trim().ToLower();

                var user = await _context.TblUsers
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.Trim().ToLower() == cleanEmail);

                if (user == null)
                {
                    return Json(new { success = false, message = _localizer["Auth_EmailNotFound"].Value });
                }

                if (!string.Equals(user.Status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = _localizer["Auth_AccountDeactivated"].Value });
                }

                string tempPassword = Guid.NewGuid().ToString("N").Substring(0, 8);
                user.Passwork = PasswordHasher.Hash(tempPassword);
                await _context.SaveChangesAsync();

                HttpContext.Session.SetInt32("TempPassword_UserId", user.UserId);
                await HttpContext.Session.CommitAsync();

                string emailSubject = _localizer["Email_ForgotSubject"].Value;
                string emailBody = $@"
                <div style='font-family: Arial, sans-serif; padding: 24px; line-height: 1.6; color: #334155; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 16px;'>
                    <h2 style='color: #0d9488; margin-top: 0;'>{_localizer["Email_ForgotTitle"].Value}</h2>
                    <p>{_localizer["Email_Greeting"].Value} <b>{user.Name ?? "Reader"}</b>,</p>
                    <p>{_localizer["Email_ForgotMessage"].Value}:</p>
                    <div style='margin: 16px 0; padding: 14px 24px; background-color: #f0fdfa; border: 1.5px dashed #0d9488; font-size: 20px; font-weight: bold; letter-spacing: 2px; color: #0f766e; display: inline-block; border-radius: 8px;'>
                        {tempPassword}
                    </div>
                    <p style='color: #e11d48; font-size: 13px; font-weight: 600;'>{_localizer["Email_ForgotNotice"].Value}</p>
                </div>";

                await emailSender.SendEmailAsync(cleanEmail, emailSubject, emailBody);

                return Json(new
                {
                    success = true,
                    message = _localizer["Auth_TempPasswordSentSuccess"].Value
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = _localizer["Email_SendError"].Value + ": " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string newPassword, string confirmPassword)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId <= 0)
            {
                return Json(new { success = false, message = _localizer["Auth_SignInFirst"].Value });
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Trim().Length < 6)
            {
                return Json(new { success = false, message = _localizer["Auth_PasswordMinLength"].Value });
            }

            if (newPassword.Trim() != (confirmPassword ?? "").Trim())
            {
                return Json(new { success = false, message = _localizer["Auth_PasswordMismatch"].Value });
            }

            var user = await _context.TblUsers.FindAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = _localizer["Auth_UserNotFound"].Value });
            }

            user.Passwork = PasswordHasher.Hash(newPassword.Trim());
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("TempPassword_UserId");

            return Json(new
            {
                success = true,
                message = _localizer["Auth_PasswordUpdatedSuccess"].Value
            });
        }

        [HttpPost]
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = _localizer["Auth_LoggedOutSuccess"].Value });
            }

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
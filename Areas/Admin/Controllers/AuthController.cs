using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuthController : Controller
    {
        private readonly TruyenSongNguContext _context;

        public AuthController(TruyenSongNguContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (HttpContext.Session.GetInt32("AdminId") != null)
            {
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            return View(new AdminLoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RateLimit("AdminLogin", maxRequests: 5, timeWindowInSeconds: 120)]
        public async Task<IActionResult> Login(AdminLoginViewModel model, string captchaAnswer)
        {
            // Kiểm tra captcha
            if (!CaptchaHelper.ValidateCaptcha(HttpContext.Session, "AdminCaptchaResult", captchaAnswer))
            {
                ModelState.AddModelError(string.Empty, "Incorrect security captcha answer!");
                return View(model);
            }

            if (!ModelState.IsValid) return View(model);

            var admin = await _context.TblAdmins
                .FirstOrDefaultAsync(a => a.Username == model.Username.Trim());

            if (admin == null || string.IsNullOrEmpty(admin.PasswordHash) || !PasswordHasher.Verify(admin.PasswordHash, model.Password))
            {
                ModelState.AddModelError(string.Empty, "Incorrect account or password!");
                return View(model);
            }

            if (!admin.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This account is currently locked!");
                return View(model);
            }

            HttpContext.Session.SetInt32("AdminId", admin.AdminId);
            HttpContext.Session.SetString("AdminUser", admin.Username);
            HttpContext.Session.SetString("AdminFullName", admin.FullName ?? admin.Username);
            HttpContext.Session.SetString("AdminRole", admin.Role ?? "Viewer");

            admin.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();
            await HttpContext.Session.CommitAsync();

            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }

        [HttpPost]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // Xóa sạch toàn bộ Session hiện tại
            HttpContext.Session.Clear();

            // Đồng bộ xóa Session trong Cache ngay lập tức
            await HttpContext.Session.CommitAsync();

            // Chuyển hướng trực tiếp về trang Login của Admin
            return RedirectToAction("Login", "Auth", new { area = "Admin" });
        }
        [HttpGet]
        public IActionResult GetAdminCaptcha()
        {
            string question = CaptchaHelper.GenerateMathCaptcha(HttpContext.Session, "AdminCaptchaResult");
            return Json(new { question });
        }
    }
}
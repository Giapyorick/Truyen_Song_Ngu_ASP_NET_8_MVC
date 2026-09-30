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
        public async Task<IActionResult> Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var admin = await _context.TblAdmins
                .FirstOrDefaultAsync(a => a.Username == model.Username.Trim());

            // Xác thực mật khẩu thông qua Argon2
            if (admin == null || string.IsNullOrEmpty(admin.PasswordHash) || !PasswordHasher.Verify(admin.PasswordHash, model.Password))
            {
                ModelState.AddModelError(string.Empty, "Incorrect account or password.!");
                return View(model);
            }

            if (!admin.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This account is currently locked.!");
                return View(model);
            }

            // Ghi nhận thông tin vào Session
            HttpContext.Session.SetInt32("AdminId", admin.AdminId);
            HttpContext.Session.SetString("AdminUser", admin.Username);
            HttpContext.Session.SetString("AdminFullName", admin.FullName ?? admin.Username);
            HttpContext.Session.SetString("AdminRole", admin.Role); // "Admin" hoặc "Viewer"

            admin.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }

        [HttpPost]
        [HttpGet]
        public IActionResult Logout()
        {
            // Xóa sạch toàn bộ Session hiện tại
            HttpContext.Session.Clear();

            // Chuyển hướng trực tiếp về trang Login của AuthController
            return RedirectToAction("Login", "Auth", new { area = "Admin" });
        }
    }
}
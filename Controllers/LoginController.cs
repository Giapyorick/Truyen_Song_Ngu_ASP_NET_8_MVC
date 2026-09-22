using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebTruyenTranh.Helpers;
using Microsoft.Extensions.Logging;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;


namespace WebTruyenTranh.Controllers
{
    
    public class LoginController : Controller
    {
        private readonly TruyenSongNguContext _context;
        public LoginController(TruyenSongNguContext context){
            _context = context;
        }
        [HttpGet]
        public IActionResult Login(){
            return View();
        }
        [HttpPost]
        public IActionResult Login(UsersViewModel model)
        {
            var user = _context.TblUsers.FirstOrDefault(x => x.Email == model.Email);

            if (user == null || !PasswordHasher.Verify(user.Passwork, model.Passwork))
            {
                return Json(new
                {
                    success = false,
                    message = "Email hoặc mật khẩu không chính xác!"
                });
            }

            // BẮT BUỘC: Lưu thông tin vào Session khi đăng nhập thành công
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.Name ?? user.Email);

            return Json(new
            {
                success = true,
                message = "Đăng nhập thành công!",
                userId = user.UserId,
                userName = user.Name
            });
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }


    }
}
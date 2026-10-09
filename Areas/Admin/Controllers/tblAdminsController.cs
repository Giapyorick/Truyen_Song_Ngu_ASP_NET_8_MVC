using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebTruyenTranh.Areas.Admin.ViewModels;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using Microsoft.Extensions.Localization; 
namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize] // Cả Admin và Viewer đều vào xem danh sách được
    public class tblAdminsController : Controller
    {
        private readonly TruyenSongNguContext _context;
        private readonly IStringLocalizer<SharedResource> _localizer; 

        public tblAdminsController(TruyenSongNguContext context, IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        // GET: /Admin/tblAdmins/Admins
        [HttpGet]
        public async Task<IActionResult> Admins()
        {
            int currentLoggedInAdminId = HttpContext.Session.GetInt32("AdminId") ?? 0;
            var currentAdmin = await _context.TblAdmins.FindAsync(currentLoggedInAdminId);

            bool isSuperAdmin = currentAdmin != null && (
                string.Equals(currentAdmin.Role?.Trim(), "Super Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentAdmin.Role?.Trim(), "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            );

            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.CurrentAdminId = currentLoggedInAdminId;

            return View();
        }

        // GET: /Admin/tblAdmins/List (Gọi AJAX)
        [HttpGet]
        public async Task<IActionResult> List(string? search, string? role, string? status, int page = 1, int pageSize = 5)
        {
            var query = _context.TblAdmins.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(x => x.Username.ToLower().Contains(search) ||
                                         (x.FullName != null && x.FullName.ToLower().Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(role) && role != "all")
            {
                query = query.Where(x => x.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                bool isActive = status == "Active";
                query = query.Where(x => x.IsActive == isActive);
            }

            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var admins = await query
                .OrderByDescending(x => x.AdminId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    adminId = x.AdminId,
                    username = x.Username,
                    fullName = x.FullName ?? "",
                    role = x.Role,
                    isActive = x.IsActive,
                    createdAt = x.CreatedAt.HasValue ? x.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                    lastLogin = x.LastLogin.HasValue ? x.LastLogin.Value.ToString("dd/MM/yyyy HH:mm") : "Not logged in"
                })
                .ToListAsync();

            return Json(new
            {
                admins,
                currentPage = page,
                totalPages
            });
        }

        // GET: /Admin/tblAdmins/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var admin = await _context.TblAdmins.FindAsync(id);
            if (admin == null) return NotFound(new { success = false, message = "Account not found!" });

            return Json(new
            {
                success = true,
                data = new
                {
                    adminId = admin.AdminId,
                    username = admin.Username,
                    fullName = admin.FullName,
                    role = admin.Role,
                    isActive = admin.IsActive
                }
            });
        }

        // POST: /Admin/tblAdmins/Add
        [HttpPost]
        [AdminRoleAuthorize] // Chặn Viewer
        public async Task<IActionResult> Add([FromBody] AdminUserViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
            {
                return Json(new { success = false, message = _localizer["AdminCommon_RequiredForNewAccount"].Value });
            }

            int currentLoggedInAdminId = HttpContext.Session.GetInt32("AdminId") ?? 0;
            var currentAdmin = await _context.TblAdmins.FindAsync(currentLoggedInAdminId);
            bool isSuperAdmin = currentAdmin != null && (
                string.Equals(currentAdmin.Role?.Trim(), "Super Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentAdmin.Role?.Trim(), "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            );

            // BẢO VỆ: Chỉ Super Admin mới được tạo tài khoản có Role Super Admin
            string targetRole = model.Role?.Trim() ?? "Viewer";
            if (targetRole.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) && !isSuperAdmin)
            {
                targetRole = "Admin"; // Hạ về Admin nếu không phải Super Admin
            }

            var exists = await _context.TblAdmins.AnyAsync(x => x.Username.ToLower() == model.Username.Trim().ToLower());
            if (exists)
            {
                return Json(new { success = false, message = _localizer["AdminAdmins_UsernameExists"].Value });
            }

            var entity = new TblAdmin
            {
                Username = model.Username.Trim(),
                PasswordHash = PasswordHasher.Hash(model.Password),
                FullName = model.FullName?.Trim(),
                Role = targetRole,
                IsActive = isSuperAdmin ? model.IsActive : true,
                CreatedAt = DateTime.Now
            };

            _context.TblAdmins.Add(entity);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Account created successfully!" });
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Update([FromBody] AdminUserViewModel model)
        {
            int currentLoggedInAdminId = HttpContext.Session.GetInt32("AdminId") ?? 0;
            string currentRole = HttpContext.Session.GetString("AdminRole") ?? "";

            // 1. Lấy thông tin tài khoản đang đăng nhập trong database để kiểm tra quyền thực tế
            var currentAdmin = await _context.TblAdmins.FindAsync(currentLoggedInAdminId);
            if (currentAdmin == null)
            {
                return Json(new { success = false, message = _localizer["AdminAdmins_AccountNotFound"].Value });
            }

            bool isSuperAdmin = string.Equals(currentAdmin.Role?.Trim(), "Super Admin", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(currentAdmin.Role?.Trim(), "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            // 2. KIỂM TRA PHÂN QUYỀN SỬA: Nếu không phải Super Admin thì CHỈ ĐƯỢC PHÉP SỬA CHÍNH MÌNH
            if (!isSuperAdmin && model.AdminId != currentLoggedInAdminId)
            {
                return Json(new
                {
                    success = false,
                    message = _localizer["AdminAdmins_NoPermissionEditOthers"].Value
                });
            }

            // 3. Tìm tài khoản mục tiêu cần cập nhật
            var entity = await _context.TblAdmins.FindAsync(model.AdminId);
            if (entity == null)
            {
                return Json(new { success = false, message = _localizer["AdminAdmins_AccountNotFound"].Value });
            }

            // Kiểm tra trùng username
            var exists = await _context.TblAdmins.AnyAsync(x => x.AdminId != model.AdminId && x.Username.ToLower() == model.Username.Trim().ToLower());
            if (exists)
            {
                return Json(new { success = false, message = _localizer["AdminAdmins_UsernameExists"].Value });
            }

            bool isPasswordChanged = !string.IsNullOrWhiteSpace(model.Password);

            // 4. XỬ LÝ ĐỔI MẬT KHẨU
            if (isPasswordChanged)
            {
                string newPassword = model.Password!.Trim();
                string confirmPassword = model.ConfirmPassword?.Trim() ?? "";

                // Kiểm tra nhập lại mật khẩu
                if (string.IsNullOrWhiteSpace(confirmPassword))
                {
                    return Json(new { success = false, message = _localizer["AdminAdmins_ConfirmPasswordRequired"].Value });
                }

                if (newPassword != confirmPassword)
                {
                    return Json(new { success = false, message = _localizer["AdminAdmins_PasswordMismatch"].Value });
                }

                // Bắt buộc nhập mật khẩu của người đang thao tác để xác thực
                if (string.IsNullOrWhiteSpace(model.CurrentAdminPassword))
                {
                    return Json(new { success = false, message = _localizer["AdminAdmins_ReqVerifyPassword"].Value });
                }

                // Kiểm tra mật khẩu tài khoản người đang đăng nhập
                if (!PasswordHasher.Verify(currentAdmin.PasswordHash, model.CurrentAdminPassword))
                {
                    return Json(new { success = false, message = _localizer["AdminAdmins_VerifyPasswordFailed"].Value });
                }

                // Cập nhật mật khẩu mã hóa Argon2
                entity.PasswordHash = PasswordHasher.Hash(newPassword);
            }

            // Cập nhật thông tin cơ bản
            entity.Username = model.Username.Trim();
            entity.FullName = model.FullName?.Trim();

            // Chỉ Super Admin mới được quyền đổi vai trò (Role) và trạng thái (IsActive) của tài khoản
            if (isSuperAdmin)
            {
                entity.Role = model.Role;
                entity.IsActive = model.IsActive;
            }

            await _context.SaveChangesAsync();

            // 5. Nếu tự đổi mật khẩu của chính mình -> Hủy phiên đăng nhập và bắt login lại
            if (isPasswordChanged && currentLoggedInAdminId == entity.AdminId)
            {
                HttpContext.Session.Clear(); // Xóa sạch Session đăng nhập
                return Json(new
                {
                    success = true,
                    requireRelogin = true,
                    message = _localizer["AdminAdmins_PasswordChangedRelogin"].Value
                });
            }

            return Json(new
            {
                success = true,
                requireRelogin = false,
                message = _localizer["AdminAdmins_UpdateAccountSuccess"].Value
            });
        }

        // POST: /Admin/tblAdmins/Delete
        [HttpPost]
        [AdminRoleAuthorize] // ❌ Chặn Viewer
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = HttpContext.Session.GetInt32("AdminId");
            if (currentUserId == id)
            {
                return Json(new { success = false, message = "You cannot delete your own account!" });
            }

            var entity = await _context.TblAdmins.FindAsync(id);
            if (entity == null) return Json(new { success = false, message = "Account not found!" });

            _context.TblAdmins.Remove(entity);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Account deleted successfully!" });
        }

        // POST: /Admin/tblAdmins/DeleteMultiple
        [HttpPost]
        [AdminRoleAuthorize] // ❌ Chặn Viewer
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any()) return Json(new { success = false, message = "Please select accounts to delete!" });

            var currentUserId = HttpContext.Session.GetInt32("AdminId") ?? 0;
            var toDelete = await _context.TblAdmins
                .Where(x => ids.Contains(x.AdminId) && x.AdminId != currentUserId)
                .ToListAsync();

            if (!toDelete.Any())
            {
                return Json(new { success = false, message = "No valid accounts to delete (Cannot delete your own account)!" });
            }

            _context.TblAdmins.RemoveRange(toDelete);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Successfully deleted {toDelete.Count} accounts!",
                deletedCount = toDelete.Count
            });
        }
    }
}
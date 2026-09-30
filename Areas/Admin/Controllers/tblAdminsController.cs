using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebTruyenTranh.Areas.Admin.ViewModels;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize] // Cả Admin và Viewer đều vào xem danh sách được
    public class tblAdminsController : Controller
    {
        private readonly TruyenSongNguContext _context;

        public tblAdminsController(TruyenSongNguContext context)
        {
            _context = context;
        }

        // GET: /Admin/tblAdmins/Admins
        [HttpGet]
        public IActionResult Admins()
        {
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
                    lastLogin = x.LastLogin.HasValue ? x.LastLogin.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa đăng nhập"
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
            if (admin == null) return NotFound(new { success = false, message = "Không tìm thấy tài khoản!" });

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
        [AdminRoleAuthorize] // ❌ Chặn Viewer
        public async Task<IActionResult> Add([FromBody] AdminUserViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
            {
                return Json(new { success = false, message = "Mật khẩu không được để trống khi tạo mới!" });
            }

            var exists = await _context.TblAdmins.AnyAsync(x => x.Username.ToLower() == model.Username.Trim().ToLower());
            if (exists)
            {
                return Json(new { success = false, message = "Tên đăng nhập này đã được sử dụng!" });
            }

            var entity = new TblAdmin
            {
                Username = model.Username.Trim(),
                PasswordHash = PasswordHasher.Hash(model.Password), // Mã hóa Argon2
                FullName = model.FullName?.Trim(),
                Role = model.Role,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now
            };

            _context.TblAdmins.Add(entity);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Thêm quản trị viên thành công!" });
        }

        // POST: /Admin/tblAdmins/Update
        [HttpPost]
        [AdminRoleAuthorize] // ❌ Chặn Viewer
        public async Task<IActionResult> Update([FromBody] AdminUserViewModel model)
        {
            var entity = await _context.TblAdmins.FindAsync(model.AdminId);
            if (entity == null) return Json(new { success = false, message = "Tài khoản không tồn tại!" });

            var exists = await _context.TblAdmins.AnyAsync(x => x.AdminId != model.AdminId && x.Username.ToLower() == model.Username.Trim().ToLower());
            if (exists)
            {
                return Json(new { success = false, message = "Tên đăng nhập này đã bị trùng lặp!" });
            }

            entity.Username = model.Username.Trim();
            entity.FullName = model.FullName?.Trim();
            entity.Role = model.Role;
            entity.IsActive = model.IsActive;

            // Nếu có nhập mật khẩu mới thì cập nhật hash
            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                entity.PasswordHash = PasswordHasher.Hash(model.Password); // Mã hóa Argon2
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Cập nhật tài khoản thành công!" });
        }

        // POST: /Admin/tblAdmins/Delete
        [HttpPost]
        [AdminRoleAuthorize] // ❌ Chặn Viewer
        public async Task<IActionResult> Delete(int id)
        {
            var currentUserId = HttpContext.Session.GetInt32("AdminId");
            if (currentUserId == id)
            {
                return Json(new { success = false, message = "Bạn không thể tự xóa tài khoản đang đăng nhập!" });
            }

            var entity = await _context.TblAdmins.FindAsync(id);
            if (entity == null) return Json(new { success = false, message = "Không tìm thấy tài khoản cần xóa!" });

            _context.TblAdmins.Remove(entity);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa tài khoản quản trị thành công!" });
        }

        // POST: /Admin/tblAdmins/DeleteMultiple
        [HttpPost]
        [AdminRoleAuthorize] // ❌ Chặn Viewer
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any()) return Json(new { success = false, message = "Vui lòng chọn tài khoản cần xóa!" });

            var currentUserId = HttpContext.Session.GetInt32("AdminId") ?? 0;
            var toDelete = await _context.TblAdmins
                .Where(x => ids.Contains(x.AdminId) && x.AdminId != currentUserId)
                .ToListAsync();

            if (!toDelete.Any())
            {
                return Json(new { success = false, message = "Không có tài khoản hợp lệ để xóa (Không thể xóa chính mình)!" });
            }

            _context.TblAdmins.RemoveRange(toDelete);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Đã xóa thành công {toDelete.Count} tài khoản!",
                deletedCount = toDelete.Count
            });
        }
    }
}
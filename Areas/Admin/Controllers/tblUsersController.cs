using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebTruyenTranh.Models;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.ViewModels;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize]
    public class tblUsersController : Controller
    {
        private readonly ILogger<tblUsersController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly TruyenSongNguContext _context;

        public tblUsersController(ILogger<tblUsersController> logger, TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Users()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _context.TblUsers
                .Select(u => new {
                    u.UserId,
                    u.Name,
                    u.Email,
                    u.Phone,
                    u.Gender,
                    u.Img,
                    u.Status,
                    DoB = u.DoB.HasValue ? u.DoB.Value.ToString("yyyy-MM-dd") : ""
                })
                .FirstOrDefaultAsync(x => x.UserId == id);

            if (user == null) return NotFound();
            return Json(user);
        }

        [HttpGet]
        public async Task<IActionResult> List(string? search, string? gender, string? status, int page = 1, int pageSize = 5)
        {
            try
            {
                var query = _context.TblUsers.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search) || u.Phone.Contains(search));
                }
                if (!string.IsNullOrEmpty(gender) && gender != "all")
                {
                    query = query.Where(u => u.Gender == gender);
                }

                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    query = query.Where(u => u.Status == status);
                }

                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                var data = await query
                    .OrderByDescending(u => u.UserId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new {
                        u.UserId,
                        u.Name,
                        u.Email,
                        u.Phone,
                        u.Gender,
                        u.Img,
                        u.Status,
                        DoB = u.DoB.HasValue ? u.DoB.Value.ToString("yyyy-MM-dd") : ""
                    })
                    .ToListAsync();

                return Json(new
                {
                    users = data,
                    currentPage = page,
                    totalPages = totalPages,
                    totalItems = totalItems
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Add(UsersViewModel user)
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Data validation error." });

            var emailInput = user.Email?.Trim();
            if (string.IsNullOrEmpty(emailInput))
            {
                return Json(new { success = false, message = "Email cannot be blank." });
            }

            // KIỂM TRA TRÙNG EMAIL KHI THÊM MỚI
            bool isEmailExist = await _context.TblUsers.AnyAsync(x => x.Email.ToLower() == emailInput.ToLower());
            if (isEmailExist)
            {
                return Json(new { success = false, message = $"Email '{emailInput}' is already in use by another user." });
            }

            string uniqueImg = "";

            if (user.formFile != null && user.formFile.Length > 0)
            {
                string folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "assets", "image", "users");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string imageName = $"User_{Guid.NewGuid()}{Path.GetExtension(user.formFile.FileName)}";
                string fullPath = Path.Combine(folderPath, imageName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await user.formFile.CopyToAsync(stream);
                }

                uniqueImg = $"assets/image/users/{imageName}";
            }

            try
            {
                var item = new TblUser
                {
                    Name = user.Name?.Trim(),
                    DoB = user.DoB,
                    Email = emailInput,
                    Phone = user.Phone?.Trim(),
                    Img = uniqueImg,
                    Gender = user.Gender,
                    CreateAd = DateTime.Now,
                    Passwork = PasswordHasher.Hash(user.Passwork),
                    Status = user.Status
                };

                await _context.TblUsers.AddAsync(item);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Added new member successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Update(UsersViewModel model)
        {
            var emailInput = model.Email?.Trim();
            if (string.IsNullOrEmpty(emailInput))
            {
                return Json(new { success = false, message = "Email cannot be blank." });
            }

            // KIỂM TRA TRÙNG EMAIL VỚI USER KHÁC KHI CẬP NHẬT
            bool isEmailExist = await _context.TblUsers
                .AnyAsync(x => x.Email.ToLower() == emailInput.ToLower() && x.UserId != model.UserId);

            if (isEmailExist)
            {
                return Json(new { success = false, message = $"Email '{emailInput}' is already used by another account." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            string? newImageFullPath = null;

            try
            {
                var user = await _context.TblUsers.FindAsync(model.UserId);
                if (user == null)
                    return Json(new { success = false, message = "Not found any member." });

                string oldImg = user.Img ?? "";
                string uniqueImg = oldImg;

                if (model.formFile != null && model.formFile.Length > 0)
                {
                    string folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "assets", "image", "users");
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                    string imageName = $"User_{Guid.NewGuid()}{Path.GetExtension(model.formFile.FileName)}";
                    newImageFullPath = Path.Combine(folderPath, imageName);

                    using (var stream = new FileStream(newImageFullPath, FileMode.Create))
                    {
                        await model.formFile.CopyToAsync(stream);
                    }

                    uniqueImg = $"assets/image/users/{imageName}";
                    user.Img = uniqueImg;
                }

                user.Name = model.Name?.Trim();
                user.Email = emailInput;
                user.Phone = model.Phone?.Trim();
                user.DoB = model.DoB;
                user.Gender = model.Gender;
                user.Status = model.Status;

                if (!string.IsNullOrWhiteSpace(model.Passwork))
                {
                    user.Passwork = PasswordHasher.Hash(model.Passwork);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (!string.IsNullOrEmpty(oldImg) && oldImg != uniqueImg)
                {
                    string oldImageFullPath = Path.Combine(_webHostEnvironment.WebRootPath, oldImg.TrimStart('/'));
                    if (System.IO.File.Exists(oldImageFullPath))
                    {
                        System.IO.File.Delete(oldImageFullPath);
                    }
                }

                return Json(new { success = true, message = "Updated member successfully!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                if (!string.IsNullOrEmpty(newImageFullPath) && System.IO.File.Exists(newImageFullPath))
                {
                    System.IO.File.Delete(newImageFullPath);
                }

                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var user = await _context.TblUsers.FindAsync(id);
                if (user == null)
                    return Json(new { success = false, message = "Not found any member." });

                _context.TblUsers.Remove(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Removed member successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "System error: " + ex.Message });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public IActionResult DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any())
                return BadRequest("No users selected.");

            var cannotDelete = new List<int>();
            var canDelete = new List<TblUser>();

            foreach (var id in ids)
            {
                bool isReferenced = _context.TblUserFollowStories.Any(x => x.UserId == id);

                if (isReferenced)
                {
                    cannotDelete.Add(id);
                }
                else
                {
                    var user = _context.TblUsers.FirstOrDefault(x => x.UserId == id);
                    if (user != null)
                        canDelete.Add(user);
                }
            }

            if (canDelete.Any())
            {
                _context.TblUsers.RemoveRange(canDelete);
                _context.SaveChanges();
            }

            return Ok(new
            {
                deleted = canDelete.Select(x => x.UserId),
                blocked = cannotDelete
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string search, string gender, string status)
        {
            var query = _context.TblUsers.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => u.Name.Contains(search));
            if (!string.IsNullOrEmpty(gender) && gender != "all")
            {
                query = query.Where(u => u.Gender == gender);
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(u => u.Status == status);
            }

            var data = await query.ToListAsync();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Users");

            int totalCols = 8;

            ws.Cells[1, 1].Value = "LIST OF USERS";
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[1, 1].Style.Font.Size = 18;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = $"Export date: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[2, 1].Style.Font.Italic = true;

            ws.Cells[3, 1].Value = $"Total users: {data.Count}";
            ws.Cells[3, 1, 3, totalCols].Merge = true;
            ws.Cells[3, 1].Style.Font.Bold = true;

            string[] headers = { "Id", "Name", "DoB", "Email", "Phone", "Gender", "CreateAt", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[5, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.Border.Top.Style =
                cell.Style.Border.Bottom.Style =
                cell.Style.Border.Left.Style =
                cell.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            }

            int row = 6;
            foreach (var u in data)
            {
                ws.Cells[row, 1].Value = u.UserId;
                ws.Cells[row, 2].Value = u.Name;

                ws.Cells[row, 3].Value = u.DoB;
                ws.Cells[row, 3].Style.Numberformat.Format = "yyyy/MM/dd";

                ws.Cells[row, 4].Value = u.Email;
                ws.Cells[row, 5].Value = u.Phone;
                ws.Cells[row, 6].Value = u.Gender;

                ws.Cells[row, 7].Value = u.CreateAd;
                ws.Cells[row, 7].Style.Numberformat.Format = "yyyy/MM/dd";

                var statusCell = ws.Cells[row, 8];
                statusCell.Value = u.Status;
                statusCell.Style.Font.Bold = true;
                statusCell.Style.Font.Color.SetColor(
                    u.Status == "Active" ? Color.Green : Color.Red
                );

                ws.Cells[row, 1, row, totalCols].Style.Border.Top.Style =
                ws.Cells[row, 1, row, totalCols].Style.Border.Bottom.Style =
                ws.Cells[row, 1, row, totalCols].Style.Border.Left.Style =
                ws.Cells[row, 1, row, totalCols].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                row++;
            }

            ws.Cells[row + 1, 1].Value = "© Leningrad";
            ws.Cells[row + 1, 1, row + 1, totalCols].Merge = true;
            ws.Cells[row + 1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row + 1, 1].Style.Font.Italic = true;
            ws.Cells[row + 1, 1].Style.Font.Color.SetColor(Color.Gray);

            ws.Cells.AutoFitColumns(12, 40);

            var fileBytes = await package.GetAsByteArrayAsync();
            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Users_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length <= 0)
                return Json(new { success = false, message = "Please select a file!" });

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                try
                {
                    using (var package = new ExcelPackage(stream))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets.FirstOrDefault();
                        if (worksheet == null || worksheet.Dimension == null)
                            return Json(new { success = false, message = "The Excel file is empty or has no worksheet." });

                        int rowCount = worksheet.Dimension.Rows;

                        // 1. Tải trước toàn bộ Email hiện có trong Database để kiểm tra
                        var existingEmailsInDb = await _context.TblUsers
                            .Select(x => x.Email.ToLower())
                            .ToListAsync();
                        var existingEmailSet = new HashSet<string>(existingEmailsInDb, StringComparer.OrdinalIgnoreCase);

                        // 2. Set theo dõi email xuất hiện trong file Excel để tránh trùng lặp nội bộ file
                        var fileEmailSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        var userList = new List<TblUser>();
                        var duplicateEmails = new List<string>();

                        for (int row = 6; row <= rowCount; row++)
                        {
                            string rawName = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                            string rawEmail = worksheet.Cells[row, 3].Value?.ToString()?.Trim();

                            // Bỏ qua các hàng trống
                            if (string.IsNullOrEmpty(rawName) || string.IsNullOrEmpty(rawEmail))
                                continue;

                            // KIỂM TRA: Email đã có trong CSDL hoặc lặp lại trong chính file Excel
                            if (existingEmailSet.Contains(rawEmail) || fileEmailSet.Contains(rawEmail))
                            {
                                duplicateEmails.Add(rawEmail);
                                continue; // Bỏ qua không thêm dòng này
                            }

                            // Đánh dấu email đã xử lý
                            fileEmailSet.Add(rawEmail);

                            var cellValue = worksheet.Cells[row, 2].Value;
                            DateOnly? dob = null;

                            if (cellValue is DateTime dt)
                            {
                                dob = DateOnly.FromDateTime(dt);
                            }
                            else if (cellValue != null && double.TryParse(cellValue.ToString(), out double d))
                            {
                                dob = DateOnly.FromDateTime(DateTime.FromOADate(d));
                            }
                            else if (!string.IsNullOrEmpty(cellValue?.ToString()))
                            {
                                if (DateTime.TryParse(cellValue.ToString(), out DateTime parsedDt))
                                {
                                    dob = DateOnly.FromDateTime(parsedDt);
                                }
                            }

                            string phoneRaw = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                            string phoneFormatted = !string.IsNullOrEmpty(phoneRaw) && !phoneRaw.StartsWith("0")
                                ? "0" + phoneRaw
                                : phoneRaw ?? "";

                            var user = new TblUser
                            {
                                Name = rawName,
                                DoB = dob,
                                Email = rawEmail,
                                Phone = phoneFormatted,
                                Gender = worksheet.Cells[row, 5].Value?.ToString()?.Trim() ?? "Other",
                                Status = worksheet.Cells[row, 6].Value?.ToString()?.Trim() ?? "Active",
                                Passwork = PasswordHasher.Hash("123456"),
                                CreateAd = DateTime.Now
                            };

                            userList.Add(user);
                        }

                        if (userList.Count > 0)
                        {
                            _context.TblUsers.AddRange(userList);
                            await _context.SaveChangesAsync();

                            string msg = $"Successfully imported {userList.Count} member(s)!";
                            if (duplicateEmails.Count > 0)
                            {
                                msg += $" (Skipped {duplicateEmails.Count} duplicate email(s): {string.Join(", ", duplicateEmails.Distinct())})";
                            }

                            return Json(new { success = true, message = msg });
                        }

                        if (duplicateEmails.Count > 0)
                        {
                            return Json(new
                            {
                                success = false,
                                message = $"No new members imported. All {duplicateEmails.Count} email(s) already exist: {string.Join(", ", duplicateEmails.Distinct())}"
                            });
                        }

                        return Json(new { success = false, message = "No valid data was found in the file." });
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = ex.Message, detail = ex.ToString() });
                }
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}
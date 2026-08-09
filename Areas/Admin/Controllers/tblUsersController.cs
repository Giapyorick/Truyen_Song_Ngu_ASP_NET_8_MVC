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

                if (!string.IsNullOrEmpty(search)) {
                    query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search) || u.Phone.Contains(search));
                }
                if (!string.IsNullOrEmpty(gender) && gender != "all") {
                    query = query.Where(u => u.Gender == gender);
                }

                if (!string.IsNullOrEmpty(status) && status != "all") {
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

                return Json(new {
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
        public async Task<IActionResult> Add(UsersViewModel user) 
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Data error" });

            string uniqueImg = ""; 
            
            if (user.UserId > 0) {
                var existingUser = await _context.TblUsers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.UserId);
                uniqueImg = existingUser?.Img ?? ""; 
            } 

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
                
                if (user.UserId > 0 && !string.IsNullOrEmpty(uniqueImg)) {
                    string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, uniqueImg.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                uniqueImg = $"assets/image/users/{imageName}";
            }
            try 
            {
                var item = new TblUser {
                    Name = user.Name,
                    DoB = user.DoB,
                    Email = user.Email,
                    Phone = user.Phone,
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
        public async Task<IActionResult> Update(UsersViewModel model)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            string? newImageFullPath = null;

            try
            {
                var user = await _context.TblUsers.FindAsync(model.UserId);

                if (user == null)
                    return Json(new { success = false, message = "Not found any member" });

                string oldImg = user.Img ?? "";
                string uniqueImg = oldImg;

                if (model.formFile != null && model.formFile.Length > 0)
                {
                    string folderPath = Path.Combine(
                        _webHostEnvironment.WebRootPath,
                        "assets","image","users");

                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    string imageName = $"User_{Guid.NewGuid()}{Path.GetExtension(model.formFile.FileName)}";
                    newImageFullPath = Path.Combine(folderPath, imageName);

                    using (var stream = new FileStream(newImageFullPath, FileMode.Create))
                    {
                        await model.formFile.CopyToAsync(stream);
                    }

                    uniqueImg = $"assets/image/users/{imageName}";
                    user.Img = uniqueImg;
                }

                user.Name = model.Name;
                user.Email = model.Email;
                user.Phone = model.Phone;
                user.DoB = model.DoB;
                user.Gender = model.Gender;
                user.Status = model.Status;

                if (!string.IsNullOrWhiteSpace(model.Passwork))
                {
                    user.Passwork = PasswordHasher.Hash(model.Passwork);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (!string.IsNullOrEmpty(oldImg) &&
                    oldImg != uniqueImg)
                {
                    string oldImageFullPath = Path.Combine(
                        _webHostEnvironment.WebRootPath,
                        oldImg.TrimStart('/'));

                    if (System.IO.File.Exists(oldImageFullPath))
                    {
                        System.IO.File.Delete(oldImageFullPath);
                    }
                }

                return Json(new
                {
                    success = true,
                    message = "Updated member successfully!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // Nếu upload ảnh mới nhưng DB rollback thì xóa ảnh mới
                if (!string.IsNullOrEmpty(newImageFullPath) &&
                    System.IO.File.Exists(newImageFullPath))
                {
                    System.IO.File.Delete(newImageFullPath);
                }

                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
        [HttpPost]
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
        public IActionResult DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any())
                return BadRequest("No users selected.");

            var cannotDelete = new List<int>();
            var canDelete = new List<TblUser>();

            foreach (var id in ids)
            {
                bool isReferenced = _context.TblUserFollowStories
                    .Any(x => x.UserId == id);

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
            if (!string.IsNullOrEmpty(gender) && gender != "all") {
                query = query.Where(u => u.Gender == gender);
            }

            if (!string.IsNullOrEmpty(status) && status != "all") {
                query = query.Where(u => u.Status == status);
            }


            var data = await query.ToListAsync();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Users");

            int totalCols = 8;

            // ===== Title =====
            ws.Cells[1, 1].Value = "LIST OF USERS";
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[1, 1].Style.Font.Size = 18;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            // ===== Sub info =====
            ws.Cells[2, 1].Value = $"Export date: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[2, 1].Style.Font.Italic = true;

            ws.Cells[3, 1].Value = $"Total users: {data.Count}";
            ws.Cells[3, 1, 3, totalCols].Merge = true;
            ws.Cells[3, 1].Style.Font.Bold = true;

            // ===== Header =====
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

            // ===== Data =====
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

                // Border từng dòng
                ws.Cells[row, 1, row, totalCols].Style.Border.Top.Style =
                ws.Cells[row, 1, row, totalCols].Style.Border.Bottom.Style =
                ws.Cells[row, 1, row, totalCols].Style.Border.Left.Style =
                ws.Cells[row, 1, row, totalCols].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                row++;
            }

            // ===== Footer =====
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
                    var userList = new List<TblUser>();

                    for (int row = 6; row <= rowCount; row++)
                    {
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

                        var user = new TblUser
                        {
                            Name = worksheet.Cells[row, 1].Value?.ToString(),
                            DoB = dob,
                            Email = worksheet.Cells[row, 3].Value?.ToString(),
                            Phone = "0" + worksheet.Cells[row, 4].Value?.ToString(),
                            Gender = worksheet.Cells[row, 5].Value?.ToString(),      
                            Status = worksheet.Cells[row, 6].Value?.ToString(),
                            // Ensure non-null password for non-nullable DB column. Use default '123456' (hashed).
                            Passwork = PasswordHasher.Hash("123456"),
                            CreateAd = DateTime.Now
                        };

                        if (!string.IsNullOrEmpty(user.Name) && !string.IsNullOrEmpty(user.Email))
                        {
                            userList.Add(user);
                        }
                    }

                        if (userList.Count > 0)
                        {
                            _context.TblUsers.AddRange(userList);
                            await _context.SaveChangesAsync();
                            return Json(new { success = true, message = $"Imported {userList.Count} members successfully!" });
                        }

                        return Json(new { success = false, message = "No valid data was found in the file." });
                    }
                }
                catch (Exception ex)
                {
                    // Return detailed error to help debugging the 500 from client
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
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
using WebTruyenTranh.Helpers;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class tblAuthorsController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly TruyenSongNguContext _context;

        public tblAuthorsController(TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }
        [HttpGet]
        public IActionResult Authors()
        {
            return View(); 
        }
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var author = await _context.TblAuthors
                .Select(u => new {
                        u.AuthorId,
                        u.AuthorName,
                        u.Email,
                        u.Country,
                        u.Gender,
                        u.Img,
                        u.Status,
                        DoB = u.DoB.HasValue ? u.DoB.Value.ToString("yyyy-MM-dd") : ""
                    })
                .FirstOrDefaultAsync(x => x.AuthorId == id);
                

            if (author == null) return NotFound();
            return Json(author);
        }

        [HttpGet]
        public async Task<IActionResult> List(string? search, string? gender, string? status, int page = 1, int pageSize = 5)
        {
            try 
            {
                var query = _context.TblAuthors.AsQueryable().AsNoTracking();

                if (!string.IsNullOrEmpty(search)) {
                    query = query.Where(u => u.AuthorName.Contains(search) || u.Email.Contains(search) || u.Country.Contains(search));
                }
                if (!string.IsNullOrEmpty(gender) && gender != "all") {
                    query = query.Where(u => u.Gender == gender);
                }

                if (!string.IsNullOrEmpty(status) && status != "all") {
                    query = query.Where(u => u.Status == status);
                }

                int totalItems = await query.CountAsync(); 
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                // Project only required fields to avoid serializing navigation properties (prevents lazy-loading / N+1 queries)
                var data = await query
                    .OrderByDescending(u => u.AuthorId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new {
                        u.AuthorId,
                        u.AuthorName,
                        u.Email,
                        u.Country,
                        u.Gender,
                        u.Img,
                        u.Status,
                        DoB = u.DoB.HasValue ? u.DoB.Value.ToString("yyyy-MM-dd") : ""
                    })
                    .ToListAsync();

                return Json(new {
                    authors = data,
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
        public async Task<IActionResult> Add(AuthorsViewModel author) 
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Data errors" });

            string uniqueImg = ""; 
            
            if (author.AuthorId > 0) {
                var existingUser = await _context.TblAuthors.AsNoTracking().FirstOrDefaultAsync(x => x.AuthorId == author.AuthorId);
                uniqueImg = existingUser?.Img ?? ""; 
            } 

            if (author.formFile != null && author.formFile.Length > 0)
            {
                string folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "assets", "image", "authors");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string imageName = $"Author_{Guid.NewGuid()}{Path.GetExtension(author.formFile.FileName)}";
                string fullPath = Path.Combine(folderPath, imageName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await author.formFile.CopyToAsync(stream);
                }
                
                if (author.AuthorId > 0 && !string.IsNullOrEmpty(uniqueImg)) {
                    string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, uniqueImg.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                uniqueImg = $"assets/image/authors/{imageName}";
            }
            try 
            {
                var item = new TblAuthor {
                    AuthorName = author.AuthorName,
                    DoB = author.DoB,
                    Gender = author.Gender,
                    Country = author.Country,
                    Email = author.Email,
                    Img = uniqueImg,
                    Status = author.Status
                };

                await _context.TblAuthors.AddAsync(item);
                await _context.SaveChangesAsync();
                
                return Json(new { success = true, message = "New author added successfully.!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An errors occurred: " + ex.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> Update(AuthorsViewModel model)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            string? newImageFullPath = null;

            try
            {
                var author = await _context.TblAuthors.FindAsync(model.AuthorId);

                if (author == null)
                    return Json(new { success = false, message = "Not found any author" });

                string oldImg = author.Img ?? "";
                string uniqueImg = oldImg;

                if (model.formFile != null && model.formFile.Length > 0)
                {
                    string folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "assets", "image", "authors");

                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    string imageName = $"Author_{Guid.NewGuid()}{Path.GetExtension(model.formFile.FileName)}";
                    newImageFullPath = Path.Combine(folderPath, imageName);

                    using (var stream = new FileStream(newImageFullPath, FileMode.Create))
                    {
                        await model.formFile.CopyToAsync(stream);
                    }

                    uniqueImg = $"assets/image/authors/{imageName}";
                    author.Img = uniqueImg;
                }

                author.AuthorName = model.AuthorName;
                author.Email = model.Email;
                author.Country = model.Country;
                author.DoB = model.DoB;
                author.Gender = model.Gender;
                author.Status = model.Status;

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
                    message = "Author updated successfully!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

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
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var author = await _context.TblAuthors.FindAsync(id);

                if (author == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Not found any author with ID {id}."
                    });
                }

                string imgPath = author.Img ?? "";

                _context.TblAuthors.Remove(author);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (!string.IsNullOrWhiteSpace(imgPath))
                {
                    string fullPath = Path.Combine(
                        _webHostEnvironment.WebRootPath,
                        imgPath.TrimStart('/'));

                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }

                return Json(new
                {
                    success = true,
                    message = "Author deleted successfully!"
                });
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                var innerMessage = ex.InnerException?.Message ?? ex.Message;

                if (innerMessage.Contains("REFERENCE constraint") ||
                    innerMessage.Contains("foreign key") ||
                    ex.ToString().Contains("FK_"))
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot delete ID {id}, because it is currently referenced by existing stories."
                    });
                }

                return Json(new
                {
                    success = false,
                    message = $"Database error: {ex.Message}",
                    innerException = innerMessage
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return Json(new
                {
                    success = false,
                    message = $"System error: {ex.Message}",
                    innerException = ex.GetBaseException().Message
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any())
                return BadRequest("No authors selected.");

            var blockedIds = await _context.TblStories
                .Where(s => s.AuthorId.HasValue && ids.Contains(s.AuthorId.Value))
                .Select(s => s.AuthorId.Value)
                .Distinct()
                .ToListAsync();

            var canDeleteIds = ids.Except(blockedIds).ToList();

            var deletedIds = new List<int>();

            if (canDeleteIds.Any())
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var authors = await _context.TblAuthors
                    .Where(a => canDeleteIds.Contains(a.AuthorId))
                    .ToListAsync();

                var imagePaths = authors
                    .Where(a => !string.IsNullOrWhiteSpace(a.Img))
                    .Select(a => a.Img!)
                    .ToList();

                try
                {
                    _context.TblAuthors.RemoveRange(authors);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    foreach (var img in imagePaths)
                    {
                        string fullPath = Path.Combine(
                            _webHostEnvironment.WebRootPath,
                            img.TrimStart('/'));

                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                        }
                    }

                    deletedIds = authors.Select(a => a.AuthorId).ToList();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return Ok(new
            {
                success = true,
                deleted = deletedIds,
                blocked = blockedIds
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string search, string gender, string status)
        {
            var query = _context.TblAuthors.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(a => a.AuthorName.Contains(search) || a.Email.Contains(search));

            if (!string.IsNullOrEmpty(gender) && gender != "all")
                query = query.Where(a => a.Gender == gender);

            if (!string.IsNullOrEmpty(status) && status != "all")
                query = query.Where(a => a.Status == status);

            var data = await query.ToListAsync();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Authors");

            int totalCols = 6;

            ws.Cells[1, 1].Value = "LIST OF AUTHORS";
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[1, 1].Style.Font.Size = 18;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = $"Export date: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[2, 1].Style.Font.Italic = true;

            ws.Cells[3, 1].Value = $"Total authors: {data.Count}";
            ws.Cells[3, 1, 3, totalCols].Merge = true;
            ws.Cells[3, 1].Style.Font.Bold = true;

            string[] headers = { "Id", "Name", "DoB", "Email", "Gender", "Country", "Status" };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[5, i + 1].Value = headers[i];
                ws.Cells[5, i + 1].Style.Font.Bold = true;
            }

            int row = 6;
            foreach (var a in data)
            {
                ws.Cells[row, 1].Value = a.AuthorId;
                ws.Cells[row, 2].Value = a.AuthorName;
                ws.Cells[row, 3].Value = a.DoB?.ToString("yyyy/MM/dd");
                ws.Cells[row, 4].Value = a.Email;
                ws.Cells[row, 5].Value = a.Gender;
                ws.Cells[row, 6].Value = a.Country;
                ws.Cells[row, 7].Value = a.Status;
                row++;
            }

            ws.Cells.AutoFitColumns();

            var fileBytes = await package.GetAsByteArrayAsync();
            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Authors_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }

        [HttpPost]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length <= 0) 
                return Json(new { success = false, message = "Please select file!" });

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[0]; 
                    int rowCount = worksheet.Dimension.Rows;
                    var authorList = new List<TblAuthor>();

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

                        var author = new TblAuthor
                        {
                            AuthorName = worksheet.Cells[row, 1].Value?.ToString(),
                            DoB = dob,
                            Email = worksheet.Cells[row, 3].Value?.ToString(),
                            Gender = worksheet.Cells[row, 4].Value?.ToString(),      
                            Country = worksheet.Cells[row, 5].Value?.ToString(),
                                 
                            Status = "Active"
                        };

                        if (!string.IsNullOrEmpty(author.AuthorName) && !string.IsNullOrEmpty(author.Email))
                        {
                            authorList.Add(author);
                        }
                    }

                    if (authorList.Count > 0)
                    {
                        _context.TblAuthors.AddRange(authorList);
                        await _context.SaveChangesAsync();
                        return Json(new { success = true, message = $"Imported {authorList.Count} author successfully!" });
                    }
                    
                    return Json(new { success = false, message = "No valid data was found in the file." });
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
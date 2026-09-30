using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize]
    public class tblStoriesController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly TruyenSongNguContext _context;

        public tblStoriesController(TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Stories() => View();

        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var story = await _context.TblStories
                .Include(s => s.TblCategoryOfStories)
                    .ThenInclude(cs => cs.Category)
                .Where(s => s.StoryId == id)
                .Select(s => new
                {
                    s.StoryId,
                    s.Title,
                    s.AuthorId,
                    PublicationDate = s.PublicationDate.HasValue
                        ? s.PublicationDate.Value.ToString("yyyy-MM-dd")
                        : "",
                    s.Img,
                    s.Description,
                    s.Status,
                    s.Lang, // Bổ sung đọc Lang từ CSDL
                    Categories = s.TblCategoryOfStories.Select(c => new
                    {
                        c.CategoryId,
                        c.Category.Name
                    })
                })
                .FirstOrDefaultAsync();

            if (story == null) return NotFound();
            return Json(story);
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoriesForSelect()
        {
            return Json(await _context.TblCategories
                .Where(x => x.Status == "Active")
                .Select(x => new {
                    id = x.CategoryId,
                    name = x.Name
                }).ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> GetAuthorsForSelect()
        {
            var authors = await _context.TblAuthors
                .Where(a => a.Status == "Active")
                .OrderBy(a => a.AuthorName)
                .Select(a => new {
                    id = a.AuthorId,
                    name = a.AuthorName
                })
                .ToListAsync();

            return Json(authors);
        }

        [HttpGet]
        public async Task<IActionResult> List(string? search, string? status, string? categoryId, int page = 1, int pageSize = 5)
        {
            try
            {
                var query = _context.TblStories.AsQueryable().AsNoTracking();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(u =>
                        u.Title.Contains(search) ||
                        (u.Author != null && u.Author.AuthorName.Contains(search)) ||
                        (u.Description != null && u.Description.Contains(search)) ||
                        (u.Lang != null && u.Lang.Contains(search)));
                }

                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    query = query.Where(u => u.Status == status);
                }
                if (!string.IsNullOrEmpty(categoryId) && categoryId != "all")
                {
                    int cateId = int.Parse(categoryId);
                    query = query.Where(u => u.TblCategoryOfStories.Any(c => c.CategoryId == cateId));
                }

                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                var stories = await query
                    .OrderByDescending(u => u.StoryId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new
                    {
                        u.StoryId,
                        u.Title,
                        AuthorName = u.Author != null ? u.Author.AuthorName : "Unknow",
                        PublicationDate = u.PublicationDate.HasValue
                            ? u.PublicationDate.Value.ToString("yyyy-MM-dd")
                            : "",
                        u.Description,
                        u.Img,
                        u.Status,
                        u.Lang, // Trả ra Lang để hiển thị badge
                        u.Likes,
                        u.Rate,
                        u.CountRate,
                        u.CountFolower,
                        Categories = u.TblCategoryOfStories
                            .Select(c => c.Category.Name)
                            .ToList()
                    })
                    .ToListAsync();

                return Json(new
                {
                    stories,
                    currentPage = page,
                    totalPages,
                    totalItems
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Add(StoriesViewModel story)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid data" });

            string imgPath = "";

            if (story.formFile != null && story.formFile.Length > 0)
            {
                string folder = Path.Combine(_webHostEnvironment.WebRootPath, "assets/image/stories");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = $"Story_{Guid.NewGuid()}{Path.GetExtension(story.formFile.FileName)}";
                string fullPath = Path.Combine(folder, fileName);

                using var stream = new FileStream(fullPath, FileMode.Create);
                await story.formFile.CopyToAsync(stream);

                imgPath = $"assets/image/stories/{fileName}";
            }

            // Chuẩn hóa chuỗi ngôn ngữ (mặc định luôn có ít nhất Tiếng Anh, Tiếng Việt)
            string cleanLang = string.IsNullOrWhiteSpace(story.Lang) ? "Tiếng Anh, Tiếng Việt" : story.Lang.Trim();

            var item = new TblStory
            {
                Title = story.Title,
                AuthorId = story.AuthorId,
                PublicationDate = story.PublicationDate,
                Img = imgPath,
                Description = story.Description,
                Status = story.Status,
                Lang = cleanLang, // Lưu Lang vào DB
                Likes = 0,
                Rate = 0,
                CountRate = 0,
                CountFolower = 0
            };

            await _context.TblStories.AddAsync(item);
            await _context.SaveChangesAsync();

            if (story.CategoryIds?.Any() == true)
            {
                var map = story.CategoryIds.Select(id => new TblCategoryOfStory
                {
                    StoryId = item.StoryId,
                    CategoryId = id
                });

                await _context.TblCategoryOfStories.AddRangeAsync(map);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, message = "New story added successfully!" });
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Update(StoriesViewModel model)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            string uniqueImg = model.Img ?? "";
            string? oldImgPath = null;
            string? newImgPath = null;

            try
            {
                var story = await _context.TblStories
                    .Include(s => s.TblCategoryOfStories)
                    .FirstOrDefaultAsync(s => s.StoryId == model.StoryId);

                if (story == null)
                    return Json(new { success = false, message = "Story not found" });

                oldImgPath = story.Img;

                if (model.formFile != null && model.formFile.Length > 0)
                {
                    string folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "assets", "image", "stories");
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                    string imageName = $"Story_{Guid.NewGuid()}{Path.GetExtension(model.formFile.FileName)}";
                    newImgPath = Path.Combine(folderPath, imageName);

                    using (var stream = new FileStream(newImgPath, FileMode.Create))
                    {
                        await model.formFile.CopyToAsync(stream);
                    }

                    uniqueImg = $"assets/image/stories/{imageName}";
                }
                else
                {
                    uniqueImg = story.Img;
                }

                story.Title = model.Title;
                story.AuthorId = model.AuthorId;
                story.PublicationDate = model.PublicationDate;
                story.Description = model.Description;
                story.Status = model.Status;
                story.Img = uniqueImg;
                story.Lang = string.IsNullOrWhiteSpace(model.Lang) ? "Tiếng Anh, Tiếng Việt" : model.Lang.Trim(); // Cập nhật Lang

                _context.TblCategoryOfStories.RemoveRange(story.TblCategoryOfStories);

                if (model.CategoryIds?.Any() == true)
                {
                    var categories = model.CategoryIds.Select(id => new TblCategoryOfStory
                    {
                        StoryId = story.StoryId,
                        CategoryId = id
                    });

                    await _context.TblCategoryOfStories.AddRangeAsync(categories);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (!string.IsNullOrEmpty(oldImgPath) && oldImgPath != uniqueImg)
                {
                    string oldFile = Path.Combine(_webHostEnvironment.WebRootPath, oldImgPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFile)) System.IO.File.Delete(oldFile);
                }

                return Json(new { success = true, message = "Story updated successfully!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                if (!string.IsNullOrEmpty(newImgPath) && System.IO.File.Exists(newImgPath))
                {
                    System.IO.File.Delete(newImgPath);
                }

                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Delete(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var story = await _context.TblStories
                    .Include(s => s.TblCategoryOfStories)
                    .FirstOrDefaultAsync(s => s.StoryId == id);

                if (story == null)
                    return Json(new { success = false, message = "Story not found." });

                string oldImg = story.Img ?? "";

                if (story.TblCategoryOfStories.Any())
                {
                    _context.TblCategoryOfStories.RemoveRange(story.TblCategoryOfStories);
                }

                _context.TblStories.Remove(story);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (!string.IsNullOrWhiteSpace(oldImg))
                {
                    string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, oldImg.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                }

                return Json(new { success = true, message = "Story deleted successfully!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var innerMessage = ex.InnerException?.Message ?? ex.Message;

                if (innerMessage.Contains("REFERENCE constraint") || innerMessage.Contains("foreign key") || ex.ToString().Contains("FK_"))
                {
                    return Json(new { success = false, message = $"Cannot delete story ID {id} because it is referenced by other data." });
                }

                return Json(new { success = false, message = $"Database error: {ex.Message}", innerException = innerMessage });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any()) return BadRequest("No stories selected.");

            var blockedIds = await _context.TblChapters
                .Where(c => ids.Contains(c.StoryId))
                .Select(c => c.StoryId)
                .Distinct()
                .ToListAsync();

            var canDeleteIds = ids.Except(blockedIds).ToList();
            var deletedIds = new List<int>();

            if (canDeleteIds.Any())
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var stories = await _context.TblStories
                        .Include(s => s.TblCategoryOfStories)
                        .Where(s => canDeleteIds.Contains(s.StoryId))
                        .ToListAsync();

                    var imagePaths = stories.Where(s => !string.IsNullOrWhiteSpace(s.Img)).Select(s => s.Img!).ToList();
                    var mappings = stories.SelectMany(s => s.TblCategoryOfStories).ToList();

                    if (mappings.Any()) _context.TblCategoryOfStories.RemoveRange(mappings);
                    _context.TblStories.RemoveRange(stories);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    foreach (var img in imagePaths)
                    {
                        string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, img.TrimStart('/'));
                        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                    }

                    deletedIds = stories.Select(s => s.StoryId).ToList();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return Json(new { success = true, deleted = deletedIds, blocked = blockedIds });
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string? search, string? status, string? categoryId)
        {
            var query = _context.TblStories.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u =>
                    u.Title.Contains(search) ||
                    (u.Author != null && u.Author.AuthorName.Contains(search)) ||
                    (u.Description != null && u.Description.Contains(search)) ||
                    (u.Lang != null && u.Lang.Contains(search)));
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(u => u.Status == status);
            }
            if (!string.IsNullOrEmpty(categoryId) && categoryId != "all")
            {
                int cateId = int.Parse(categoryId);
                query = query.Where(u => u.TblCategoryOfStories.Any(c => c.CategoryId == cateId));
            }

            var data = await query
                .OrderByDescending(s => s.StoryId)
                .Select(s => new {
                    s.StoryId,
                    s.Title,
                    s.Description,
                    s.AuthorId,
                    AuthorName = s.Author != null ? s.Author.AuthorName : "Unknow",
                    PublicationDate = s.PublicationDate,
                    s.Likes,
                    s.Rate,
                    s.CountFolower,
                    s.CountRate,
                    s.Status,
                    s.Lang, // Export thêm cột Lang
                    Categories = s.TblCategoryOfStories.Select(c => c.Category.Name).ToList()
                })
                .ToListAsync();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Stories");

            int totalCols = 13;

            ws.Cells[1, 1].Value = "LIST OF STORIES";
            ws.Cells[1, 1, 1, totalCols].Merge = true;
            ws.Cells[1, 1].Style.Font.Size = 18;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = $"Export date: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            ws.Cells[2, 1, 2, totalCols].Merge = true;
            ws.Cells[2, 1].Style.Font.Italic = true;

            ws.Cells[3, 1].Value = $"Total stories: {data.Count}";
            ws.Cells[3, 1, 3, totalCols].Merge = true;
            ws.Cells[3, 1].Style.Font.Bold = true;

            string[] headers =
            {
                "StoryId",
                "Title",
                "Description",
                "AuthorId",
                "AuthorName",
                "Languages", // Cột Ngôn ngữ
                "PublicationDate",
                "Likes",
                "Rate",
                "CountFollower",
                "CountRate",
                "Status",
                "Categories"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[5, i + 1].Value = headers[i];
                ws.Cells[5, i + 1].Style.Font.Bold = true;
                ws.Cells[5, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[5, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                ws.Cells[5, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            int row = 6;
            foreach (var s in data)
            {
                ws.Cells[row, 1].Value = s.StoryId;
                ws.Cells[row, 2].Value = s.Title;
                ws.Cells[row, 3].Value = s.Description;
                ws.Cells[row, 4].Value = s.AuthorId;
                ws.Cells[row, 5].Value = s.AuthorName;
                ws.Cells[row, 6].Value = s.Lang ?? "Tiếng Anh, Tiếng Việt";
                ws.Cells[row, 7].Value = s.PublicationDate?.ToString("yyyy-MM-dd");
                ws.Cells[row, 8].Value = s.Likes;
                ws.Cells[row, 9].Value = s.Rate;
                ws.Cells[row, 10].Value = s.CountFolower;
                ws.Cells[row, 11].Value = s.CountRate;
                ws.Cells[row, 12].Value = s.Status;
                ws.Cells[row, 13].Value = s.Categories != null ? string.Join(", ", s.Categories) : string.Empty;

                row++;
            }

            ws.Cells.AutoFitColumns();

            var fileBytes = await package.GetAsByteArrayAsync();
            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Stories_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> ImportFromExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "File is empty" });

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets[0];
            int rowCount = worksheet.Dimension.Rows;

            var authors = await _context.TblAuthors.ToListAsync();
            var categories = await _context.TblCategories.ToListAsync();

            try
            {
                for (int row = 6; row <= rowCount; row++)
                {
                    string title = worksheet.Cells[row, 1].Text.Trim();
                    if (string.IsNullOrEmpty(title)) continue;

                    string description = worksheet.Cells[row, 2].Text.Trim();
                    string authorIdText = worksheet.Cells[row, 3].Text.Trim();

                    int? authorId = null;
                    if (int.TryParse(authorIdText, out int a) && authors.Any(x => x.AuthorId == a))
                        authorId = a;

                    string langText = worksheet.Cells[row, 5].Text.Trim();
                    if (string.IsNullOrWhiteSpace(langText)) langText = "Tiếng Anh, Tiếng Việt";

                    DateOnly? pubDate = null;
                    if (DateTime.TryParse(worksheet.Cells[row, 6].Text, out var dt))
                        pubDate = DateOnly.FromDateTime(dt);

                    string status = worksheet.Cells[row, 7].Text.Trim();
                    if (string.IsNullOrEmpty(status)) status = "Posting";

                    string categoryText = worksheet.Cells[row, 8].Text.Trim();

                    var story = new TblStory
                    {
                        Title = title,
                        Description = description,
                        AuthorId = authorId,
                        PublicationDate = pubDate,
                        Status = status,
                        Lang = langText, // Nạp trường Lang từ Excel
                        Img = null,
                        Likes = 0,
                        Rate = 0,
                        CountRate = 0,
                        CountFolower = 0
                    };

                    _context.TblStories.Add(story);
                    await _context.SaveChangesAsync();

                    var categoryNames = categoryText
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim());

                    var categoryIds = categories
                        .Where(c => categoryNames.Any(n => string.Equals(n, c.Name, StringComparison.OrdinalIgnoreCase)))
                        .Select(c => c.CategoryId)
                        .ToList();

                    if (categoryIds.Any())
                    {
                        _context.TblCategoryOfStories.AddRange(
                            categoryIds.Select(cid => new TblCategoryOfStory
                            {
                                StoryId = story.StoryId,
                                CategoryId = cid
                            })
                        );
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Import failed",
                    error = ex.Message,
                    stack = ex.InnerException?.Message
                });
            }

            return Json(new { success = true, message = "Excel imported successfully!" });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View("Error!");
    }
}
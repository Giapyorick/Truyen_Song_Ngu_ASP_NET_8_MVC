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
                .Include(s => s.TblStoryTranslations)
                .Include(s => s.TblCategoryOfStories).ThenInclude(cs => cs.Category)
                .Where(s => s.StoryId == id)
                .FirstOrDefaultAsync();

            if (story == null) return NotFound();

            var transVi = story.TblStoryTranslations.FirstOrDefault(t => t.LanguageCode == "vi-VN");

            return Json(new
            {
                story.StoryId,
                TitleEn = story.Title ?? "",           // Lấy thẳng từ bảng gốc
                DescEn = story.Description ?? "",      // Lấy thẳng từ bảng gốc
                TitleVi = transVi?.Title ?? "",        // Lấy từ bảng dịch tiếng Việt
                DescVi = transVi?.Description ?? "",
                story.AuthorId,
                PublicationDate = story.PublicationDate?.ToString("yyyy-MM-dd") ?? "",
                story.Img,
                story.Status,
                story.Lang,
                Categories = story.TblCategoryOfStories.Select(c => new { c.CategoryId, c.Category.Name })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoriesForSelect(string? culture)
        {
            var rawCulture = !string.IsNullOrWhiteSpace(culture)
                ? culture
                : System.Globalization.CultureInfo.CurrentUICulture.Name;
            var currentCulture = rawCulture.ToLower().StartsWith("vi") ? "vi-VN" : "en-US";

            var categories = await _context.TblCategories
                .AsNoTracking()
                .Where(x => x.Status == "Active")
                .Select(x => new {
                    id = x.CategoryId,
                    name = x.TblCategoryTranslations
                            .Where(t => t.LanguageCode == currentCulture)
                            .Select(t => t.Name)
                            .FirstOrDefault() ?? x.Name
                })
                .OrderBy(x => x.name)
                .ToListAsync();

            return Json(categories);
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
        public async Task<IActionResult> List(string? search, string? status, string? categoryId, string? culture, int page = 1, int pageSize = 5)
        {
            try
            {
                // 1. Chuẩn hóa culture
                var rawCulture = !string.IsNullOrWhiteSpace(culture)
                    ? culture.Trim()
                    : System.Globalization.CultureInfo.CurrentUICulture.Name;

                var currentCulture = rawCulture.ToLower().StartsWith("vi") ? "vi-VN" : "en-US";
                bool isVi = currentCulture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

                var query = _context.TblStories.AsNoTracking().AsQueryable();

                // 2. Lọc theo Status
                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    query = query.Where(u => u.Status == status);
                }

                // 3. Lọc theo Category
                if (!string.IsNullOrEmpty(categoryId) && categoryId != "all")
                {
                    if (int.TryParse(categoryId, out int cateId))
                    {
                        query = query.Where(u => u.TblCategoryOfStories.Any(c => c.CategoryId == cateId));
                    }
                }

                // 4. Lọc tìm kiếm theo Title (theo ngôn ngữ) hoặc Tác giả
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim();
                    if (isVi)
                    {
                        query = query.Where(u =>
                            u.TblStoryTranslations.Any(t => t.LanguageCode.StartsWith("vi") && t.Title.Contains(search))
                            || u.Title.Contains(search)
                            || (u.Author != null && u.Author.AuthorName.Contains(search))
                        );
                    }
                    else
                    {
                        query = query.Where(u =>
                            u.Title.Contains(search)
                            || (u.Author != null && u.Author.AuthorName.Contains(search))
                        );
                    }
                }

                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
                if (totalPages == 0) totalPages = 1;

                // 5. Phân trang và chiếu dữ liệu (Projection an toàn chống văng lỗi Navigation)
                var stories = await query
                    .OrderByDescending(u => u.StoryId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new
                    {
                        s.StoryId,

                        // Tiêu đề: Nếu Tiếng Việt bốc từ TblStoryTranslations, nếu Tiếng Anh bốc thẳng cột gốc s.Title
                        Title = isVi
                            ? (s.TblStoryTranslations
                                .Where(t => t.LanguageCode.StartsWith("vi"))
                                .Select(t => t.Title)
                                .FirstOrDefault() ?? s.Title)
                            : s.Title,

                        // Mô tả: Nếu Tiếng Việt bốc từ TblStoryTranslations, nếu Tiếng Anh bốc thẳng cột gốc s.Description
                        Description = isVi
                            ? (s.TblStoryTranslations
                                .Where(t => t.LanguageCode.StartsWith("vi"))
                                .Select(t => t.Description)
                                .FirstOrDefault() ?? s.Description)
                            : s.Description,

                        AuthorName = s.Author != null ? s.Author.AuthorName : "Unknown",
                        PublicationDate = s.PublicationDate.HasValue ? s.PublicationDate.Value.ToString("yyyy-MM-dd") : "",
                        s.Img,
                        s.Status,
                        s.Lang,
                        s.Likes,
                        s.Rate,
                        s.CountRate,
                        s.CountFolower,

                        // Lấy danh sách tên thể loại an toàn, xử lý null
                        Categories = s.TblCategoryOfStories
                            .Where(cs => cs.Category != null)
                            .Select(cs => isVi
                                ? (cs.Category.TblCategoryTranslations
                                    .Where(ct => ct.LanguageCode.StartsWith("vi"))
                                    .Select(ct => ct.Name)
                                    .FirstOrDefault() ?? cs.Category.Name)
                                : cs.Category.Name)
                            .ToList()
                    })
                    .ToListAsync();

                return Json(new
                {
                    stories,
                    currentPage = page,
                    totalPages,
                    totalItems,
                    debugCulture = currentCulture
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR Stories List]: {ex.Message} \n {ex.StackTrace}");
                return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message });
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
                if (!string.IsNullOrWhiteSpace(model.Lang))
                {
                    story.Lang = model.Lang.Trim();
                }

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
        public async Task<IActionResult> GetStoriesInfinite(string? search, string type = "likes", int? categoryId = null, int page = 1, int pageSize = 8)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            // 1. Nhận diện ngôn ngữ hiện tại
            var rqCulture = HttpContext.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>();
            var currentCulture = rqCulture?.RequestCulture.UICulture.Name
                                 ?? Request.Cookies[Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName]
                                 ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
            bool isVi = currentCulture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

            var query = _context.TblStories.AsNoTracking();

            // 2. Lọc theo thể loại
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(s => s.TblCategoryOfStories.Any(cs => cs.CategoryId == categoryId.Value));
            }

            // 3. TÌM KIẾM THEO ĐÚNG CHUẨN CSDL:
            // - EN: Tìm trực tiếp trong Title bảng gốc tblStory
            // - VN: Tìm trong Title bảng tblStoryTranslation (vi-VN)
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                if (isVi)
                {
                    query = query.Where(s =>
                        s.TblStoryTranslations.Any(t => t.LanguageCode.StartsWith("vi") && t.Title.Contains(search))
                        || s.Title.Contains(search)
                    );
                }
                else
                {
                    // Tiếng Anh: Chỉ tìm thẳng trên Title bảng gốc tblStory
                    query = query.Where(s => s.Title.Contains(search));
                }
            }

            // 4. Sắp xếp
            query = (type?.ToLower()) switch
            {
                "rate" => query.OrderByDescending(s => s.Rate).ThenByDescending(s => s.CountRate).ThenByDescending(s => s.StoryId),
                "follower" => query.OrderByDescending(s => s.CountFolower).ThenByDescending(s => s.Likes).ThenByDescending(s => s.StoryId),
                "most_rated" => query.OrderByDescending(s => s.CountRate).ThenByDescending(s => s.Rate).ThenByDescending(s => s.StoryId),
                _ => query.OrderByDescending(s => s.Likes).ThenByDescending(s => s.CountFolower).ThenByDescending(s => s.StoryId)
            };

            // 5. ÁNH XẠ DỮ LIỆU:
            // - EN: Bốc thẳng s.Title và ch.Title từ bảng gốc
            // - VN: Bốc từ TblStoryTranslations / TblChapterTranslations (LanguageCode == vi-VN)
            var stories = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    Story = s,
                    TranslatedStoryTitle = isVi
                        ? (s.TblStoryTranslations
                            .Where(st => st.LanguageCode.StartsWith("vi"))
                            .Select(st => st.Title)
                            .FirstOrDefault() ?? s.Title)
                        : s.Title, // Tiếng Anh: Lấy thẳng bảng gốc tblStory

                    Progress = _context.TblUserReadingProgresses
                        .Where(p => p.UserId == userId && p.StoryId == s.StoryId)
                        .Select(p => new
                        {
                            p.LastChapterId,
                            ChapterNumber = _context.TblChapters
                                .Where(c => c.ChapterId == p.LastChapterId)
                                .Select(c => (int?)c.ChapterNumber)
                                .FirstOrDefault()
                        })
                        .FirstOrDefault(),

                    LatestChapters = _context.TblChapters
                        .Where(ch => ch.StoryId == s.StoryId)
                        .OrderByDescending(ch => ch.ChapterNumber)
                        .Take(3)
                        .Select(ch => new LatestChapterItemViewModel
                        {
                            ChapterId = ch.ChapterId,
                            ChapterNumber = ch.ChapterNumber,
                            ChapterTitle = isVi
                                ? (ch.TblChapterTranslations
                                    .Where(ct => ct.LanguageCode.StartsWith("vi"))
                                    .Select(ct => ct.Title)
                                    .FirstOrDefault() ?? ch.Title)
                                : ch.Title // Tiếng Anh: Lấy thẳng bảng gốc tblChapter
                        }).ToList()
                })
                .Select(x => new StoryListViewModel
                {
                    StoryID = x.Story.StoryId,
                    Title = x.TranslatedStoryTitle ?? "Undefined",
                    Img = x.Story.Img,
                    Lang = x.Story.Lang,
                    Rate = x.Story.Rate,
                    Likes = x.Story.Likes,
                    CountFolower = x.Story.CountFolower,
                    CountRate = x.Story.CountRate,
                    HasProgress = x.Progress != null,
                    LastChapterId = x.Progress != null ? x.Progress.LastChapterId : null,
                    LastChapterNumber = x.Progress != null ? x.Progress.ChapterNumber : null,
                    LatestChapters = x.LatestChapters
                })
                .ToListAsync();

            return PartialView("_StoryCardsPartial", stories);
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string? search, string? status, string? categoryId)
        {
            var currentCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            var query = _context.TblStories.AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(u => u.Status == status);
            }
            if (!string.IsNullOrEmpty(categoryId) && categoryId != "all")
            {
                int cateId = int.Parse(categoryId);
                query = query.Where(u => u.TblCategoryOfStories.Any(c => c.CategoryId == cateId));
            }

            var projected = query.Select(s => new {
                s.StoryId,
                Title = s.TblStoryTranslations.Where(t => t.LanguageCode == currentCulture).Select(t => t.Title).FirstOrDefault()
                        ?? s.TblStoryTranslations.Select(t => t.Title).FirstOrDefault()
                        ?? s.Title,
                Description = s.TblStoryTranslations.Where(t => t.LanguageCode == currentCulture).Select(t => t.Description).FirstOrDefault()
                              ?? s.TblStoryTranslations.Select(t => t.Description).FirstOrDefault()
                              ?? s.Description,
                s.AuthorId,
                AuthorName = s.Author != null ? s.Author.AuthorName : "Unknown",
                PublicationDate = s.PublicationDate,
                s.Likes,
                s.Rate,
                s.CountFolower,
                s.CountRate,
                s.Status,
                s.Lang,
                Categories = s.TblCategoryOfStories.Select(c => c.Category.Name).ToList()
            });

            if (!string.IsNullOrEmpty(search))
            {
                projected = projected.Where(u =>
                    u.Title.Contains(search) ||
                    u.AuthorName.Contains(search) ||
                    (u.Description != null && u.Description.Contains(search)));
            }

            var data = await projected.OrderByDescending(s => s.StoryId).ToListAsync();

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

            string[] headers = { "StoryId", "Title", "Description", "AuthorId", "AuthorName", "Languages", "PublicationDate", "Likes", "Rate", "CountFollower", "CountRate", "Status", "Categories" };

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
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Stories_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> TranslateWithAi(string text, string fromLang, string toLang, [FromServices] IAiTranslationService aiService)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Json(new { success = false, message = "The original content to be translated is empty!" });

            try
            {
                var translated = await aiService.TranslateAsync(text, fromLang, toLang);

                if (string.IsNullOrWhiteSpace(translated))
                {
                    return Json(new { success = false, message = "The AI ​​did not return a translation result!" });
                }

                return Json(new { success = true, result = translated });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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
        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> SaveStory(
    [FromForm] StoriesViewModel model,
    [FromForm] string? TitleVi, [FromForm] string? DescVi,
    [FromForm] string? TitleEn, [FromForm] string? DescEn,
    [FromForm] List<int>? CategoryIds)
        {
            try
            {
                var isNew = model.StoryId <= 0;
                TblStory story;
                string? oldImgPath = null;
                string? newImgRelativePath = null;

                // 1. Chuẩn hóa chuỗi nhập
                var cleanTitleEn = !string.IsNullOrWhiteSpace(TitleEn) ? TitleEn.Trim() : (!string.IsNullOrWhiteSpace(model.Title) ? model.Title.Trim() : "");
                var cleanDescEn = !string.IsNullOrWhiteSpace(DescEn) ? DescEn.Trim() : (!string.IsNullOrWhiteSpace(model.Description) ? model.Description.Trim() : "");
                var cleanTitleVi = !string.IsNullOrWhiteSpace(TitleVi) ? TitleVi.Trim() : "";
                var cleanDescVi = !string.IsNullOrWhiteSpace(DescVi) ? DescVi.Trim() : "";

                // Fallback: Nếu thiếu tiếng Anh thì lấy tiếng Việt bù vào để bảng gốc luôn có dữ liệu
                if (string.IsNullOrWhiteSpace(cleanTitleEn)) cleanTitleEn = cleanTitleVi;
                if (string.IsNullOrWhiteSpace(cleanDescEn)) cleanDescEn = cleanDescVi;

                if (string.IsNullOrWhiteSpace(cleanTitleEn))
                {
                    return Json(new { success = false, message = "Vui lòng nhập tiêu đề truyện!" });
                }

                // 2. Upload ảnh bìa nếu có chọn file
                if (model.formFile != null && model.formFile.Length > 0)
                {
                    string folder = Path.Combine(_webHostEnvironment.WebRootPath, "assets", "image", "stories");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string fileName = $"Story_{Guid.NewGuid()}{Path.GetExtension(model.formFile.FileName)}";
                    string fullPath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await model.formFile.CopyToAsync(stream);
                    }
                    newImgRelativePath = $"assets/image/stories/{fileName}";
                }

                string cleanLang = string.IsNullOrWhiteSpace(model.Lang) ? "Tiếng Anh, Tiếng Việt" : model.Lang.Trim();
                int? authorId = (model.AuthorId.HasValue && model.AuthorId.Value > 0) ? model.AuthorId : null;
                string cleanStatus = string.IsNullOrWhiteSpace(model.Status) ? "Completed" : model.Status.Trim();

                // 3. Thêm mới hoặc Cập nhật bảng gốc tblStory
                if (isNew)
                {
                    story = new TblStory
                    {
                        Title = cleanTitleEn,
                        Description = cleanDescEn,
                        AuthorId = authorId,
                        PublicationDate = model.PublicationDate,
                        Status = cleanStatus,
                        Img = newImgRelativePath ?? "",
                        Lang = cleanLang,
                        Likes = 0,
                        Rate = 0,
                        CountRate = 0,
                        CountFolower = 0
                    };

                    await _context.TblStories.AddAsync(story);
                    await _context.SaveChangesAsync(); // Lưu để có StoryId
                }
                else
                {
                    story = await _context.TblStories
                        .Include(s => s.TblCategoryOfStories)
                        .Include(s => s.TblStoryTranslations)
                        .FirstOrDefaultAsync(s => s.StoryId == model.StoryId);

                    if (story == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy truyện cần sửa!" });
                    }

                    oldImgPath = story.Img;

                    story.Title = cleanTitleEn;
                    story.Description = cleanDescEn;
                    story.AuthorId = authorId;
                    story.PublicationDate = model.PublicationDate;
                    story.Status = cleanStatus;
                    story.Lang = cleanLang;

                    if (!string.IsNullOrEmpty(newImgRelativePath))
                    {
                        story.Img = newImgRelativePath;
                    }
                }

                // 4. CẬP NHẬT DANH MỤC THỂ LOẠI (AN TOÀN TUYỆT ĐỐI)
                var selectedCates = CategoryIds ?? model.CategoryIds ?? new List<int>();
                var validCateIds = selectedCates.Where(id => id > 0).Distinct().ToList();

                // Xóa các liên kết thể loại cũ
                var existingCategories = await _context.TblCategoryOfStories
                    .Where(cs => cs.StoryId == story.StoryId)
                    .ToListAsync();
                if (existingCategories.Any())
                {
                    _context.TblCategoryOfStories.RemoveRange(existingCategories);
                }

                // Thêm liên kết thể loại mới
                if (validCateIds.Any())
                {
                    var newMappings = validCateIds.Select(cid => new TblCategoryOfStory
                    {
                        StoryId = story.StoryId,
                        CategoryId = cid
                    });
                    await _context.TblCategoryOfStories.AddRangeAsync(newMappings);
                }

                // 5. CẬP NHẬT BẢNG DỊCH TIẾNG VIỆT (tblStoryTranslation)
                if (!string.IsNullOrWhiteSpace(cleanTitleVi))
                {
                    var transVi = await _context.TblStoryTranslations
                        .FirstOrDefaultAsync(t => t.StoryId == story.StoryId && t.LanguageCode == "vi-VN");

                    if (transVi == null)
                    {
                        _context.TblStoryTranslations.Add(new TblStoryTranslation
                        {
                            StoryId = story.StoryId,
                            LanguageCode = "vi-VN",
                            Title = cleanTitleVi,
                            Description = cleanDescVi
                        });
                    }
                    else
                    {
                        transVi.Title = cleanTitleVi;
                        transVi.Description = cleanDescVi;
                    }
                }

                await _context.SaveChangesAsync();

                // Xóa ảnh cũ trên ổ đĩa nếu đã upload ảnh mới
                if (!string.IsNullOrEmpty(oldImgPath) && !string.IsNullOrEmpty(newImgRelativePath) && oldImgPath != newImgRelativePath)
                {
                    string oldDiskFile = Path.Combine(_webHostEnvironment.WebRootPath, oldImgPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldDiskFile))
                    {
                        try { System.IO.File.Delete(oldDiskFile); } catch { }
                    }
                }

                return Json(new { success = true, message = isNew ? "Thêm truyện thành công!" : "Cập nhật truyện thành công!" });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                System.Diagnostics.Debug.WriteLine($"[SAVE STORY ERROR]: {ex.Message} - {inner}");
                return Json(new { success = false, message = "Lỗi lưu dữ liệu: " + inner });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View("Error!");
    }

}
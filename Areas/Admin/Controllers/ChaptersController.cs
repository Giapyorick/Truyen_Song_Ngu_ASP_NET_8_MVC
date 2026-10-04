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
    [AdminAuthorize]
    public class tblChaptersController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly TruyenSongNguContext _context;

        public tblChaptersController(TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }
        [HttpGet]
        public IActionResult Chapters()
        {
            return View(); 
        }
        [HttpGet]
        public async Task<IActionResult> GetStoriesForSelect(string? culture)
        {
            var rawCulture = !string.IsNullOrWhiteSpace(culture)
                ? culture.Trim()
                : System.Globalization.CultureInfo.CurrentUICulture.Name;
            var currentCulture = rawCulture.ToLower().StartsWith("vi") ? "vi-VN" : "en-US";
            string langPrefix = currentCulture.StartsWith("vi") ? "vi" : "en";

            var stories = await _context.TblStories
                .AsNoTracking()
                .Select(s => new
                {
                    id = s.StoryId,
                    name = s.TblStoryTranslations
                            .Where(t => t.LanguageCode.StartsWith(langPrefix))
                            .Select(t => t.Title)
                            .FirstOrDefault() ?? s.Title
                })
                .OrderBy(s => s.name)
                .ToListAsync();

            return Json(stories);
        }
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var chapter = await _context.TblChapters
                .Include(x => x.Story)
                .Include(x => x.TblChapterTranslations)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ChapterId == id);

            if (chapter == null) return NotFound();

            var transVi = chapter.TblChapterTranslations.FirstOrDefault(t => t.LanguageCode == "vi-VN");

            return Json(new
            {
                chapter.ChapterId,
                TitleEn = chapter.Title ?? "",      // Tiếng Anh lấy thẳng bảng gốc
                TitleVi = transVi?.Title ?? "",     // Tiếng Việt lấy từ bảng translation
                chapter.StoryId,
                chapter.ChapterNumber,
                CreateDate = chapter.CreatedDate.HasValue ? chapter.CreatedDate.Value.ToString("yyyy-MM-dd") : ""
            });
        }

        [HttpGet]
        public async Task<IActionResult> List(string? search, string? storyId, string? culture, int page = 1, int pageSize = 5)
        {
            try
            {
                var rawCulture = !string.IsNullOrWhiteSpace(culture)
                    ? culture.Trim()
                    : System.Globalization.CultureInfo.CurrentUICulture.Name;
                var currentCulture = rawCulture.ToLower().StartsWith("vi") ? "vi-VN" : "en-US";
                string langPrefix = currentCulture.StartsWith("vi") ? "vi" : "en";

                var query = _context.TblChapters.AsNoTracking().AsQueryable();

                if (!string.IsNullOrEmpty(storyId) && storyId != "all")
                {
                    int stId = int.Parse(storyId);
                    query = query.Where(x => x.StoryId == stId);
                }

                var projectedQuery = query.Select(c => new
                {
                    c.ChapterId,
                    // Tiêu đề chương song ngữ
                    Title = c.TblChapterTranslations
                             .Where(t => t.LanguageCode.StartsWith(langPrefix))
                             .Select(t => t.Title)
                             .FirstOrDefault() ?? c.Title,

                    c.StoryId,
                    // Tên truyện song ngữ
                    StoryTitle = c.Story.TblStoryTranslations
                                  .Where(st => st.LanguageCode.StartsWith(langPrefix))
                                  .Select(st => st.Title)
                                  .FirstOrDefault() ?? c.Story.Title,

                    c.ChapterNumber,
                    CreateDate = c.CreatedDate.HasValue ? c.CreatedDate.Value.ToString("yyyy-MM-dd") : ""
                });

                if (!string.IsNullOrEmpty(search))
                {
                    projectedQuery = projectedQuery.Where(x => x.Title.Contains(search) || x.StoryTitle.Contains(search));
                }

                int totalItems = await projectedQuery.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                var data = await projectedQuery
                    .OrderByDescending(u => u.StoryId)
                    .ThenBy(x => x.ChapterNumber)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Json(new
                {
                    chapters = data,
                    currentPage = page,
                    totalPages,
                    totalItems,
                    debugCulture = currentCulture
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Add(ChaptersViewModel chapter) 
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Data errors" });
            try 
            {
                var item = new TblChapter {
                    Title = chapter.Title,
                    StoryId = chapter.StoryId,
                    ChapterNumber = chapter.ChapterNumber,
                    CreatedDate = DateTime.Now
                };

                await _context.TblChapters.AddAsync(item);
                await _context.SaveChangesAsync();
                
                return Json(new { success = true, message = "New chapter added successfully.!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An errors occurred: " + ex.Message });
            }
        }
        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Update(ChaptersViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid data" });

            var chapter = await _context.TblChapters
                .FirstOrDefaultAsync(x => x.ChapterId == model.ChapterId);

            if (chapter == null)
                return Json(new { success = false, message = "Chapter not found" });

            chapter.Title = model.Title;
            chapter.StoryId = model.StoryId;
            chapter.ChapterNumber = model.ChapterNumber;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Chapter updated successfully!" });
        }


        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Delete(int id)
        {
            var chapter = await _context.TblChapters.FindAsync(id);
            if (chapter == null)
                return Json(new { success = false, message = "Chapter not found" });

            _context.TblChapters.Remove(chapter);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Deleted successfully" });
        }
        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any())
                return BadRequest("No chapters selected");

            var chapters = await _context.TblChapters
                .Where(x => ids.Contains(x.ChapterId))
                .ToListAsync();

            if (!chapters.Any())
                return Json(new { success = false, message = "No valid chapters found" });

            _context.TblChapters.RemoveRange(chapters);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                deleted = chapters.Select(x => x.ChapterId)
            });
        }
        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string? search, string? storyId)
        {
            var query = _context.TblChapters
                .Include(c => c.Story)
                .OrderBy(c => c.StoryId)
                .ThenBy(c => c.ChapterNumber)
                .Select(c => new
                {
                    c.ChapterId,
                    c.Title,
                    c.StoryId,
                    StoryTitle = c.Story.Title,
                    c.ChapterNumber,
                    CreatedDate = c.CreatedDate
                }).AsQueryable();

                if (!string.IsNullOrEmpty(search))
                    query = query.Where(x => x.Title.Contains(search) || x.StoryTitle.Contains(search));

                if (!string.IsNullOrEmpty(storyId) && storyId != "all") {
                    int stId = int.Parse(storyId);
                    query = query.Where(u => u.StoryId == stId);
                }
                var data = await query.ToListAsync();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Chapters");
            int totalCols = 6;

                    ws.Cells[1, 1].Value = "LIST OF CHAPTERS";
                    ws.Cells[1, 1, 1, totalCols].Merge = true;
                    ws.Cells[1, 1].Style.Font.Size = 18;
                    ws.Cells[1, 1].Style.Font.Bold = true;
                    ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    ws.Cells[2, 1].Value = $"Export date: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    ws.Cells[2, 1, 2, totalCols].Merge = true;
                    ws.Cells[2, 1].Style.Font.Italic = true;

                    ws.Cells[3, 1].Value = $"Total chapters: {data.Count}";
                    ws.Cells[3, 1, 3, totalCols].Merge = true;
                    ws.Cells[3, 1].Style.Font.Bold = true;


            string[] headers = { "Id", "Title", "StoryId", "StoryTitle", "Chapter Number", "Created Date" };

            for (int i = 0; i < headers.Length; i++)
                ws.Cells[5, i + 1].Value = headers[i];

            int row = 6;
            foreach (var c in data)
            {
                ws.Cells[row, 1].Value = c.ChapterId;
                ws.Cells[row, 2].Value = c.Title;
                ws.Cells[row, 3].Value = c.StoryId;
                ws.Cells[row, 4].Value = c.StoryTitle;
                ws.Cells[row, 5].Value = c.ChapterNumber;
                ws.Cells[row, 6].Value = c.CreatedDate?.ToString("yyyy-MM-dd");
                row++;
            }

            ws.Cells.AutoFitColumns();

            return File(
                await package.GetAsByteArrayAsync(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Chapters_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> ImportToExcel(IFormFile file)
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
                    var chapterList = new List<TblChapter>();
                    var chapters = await _context.TblChapters.ToListAsync();

                    for (int row = 6; row <= rowCount; row++)
                    {
                        if (!int.TryParse(worksheet.Cells[row, 2].Text, out int storyId))
                            continue;

                        if (!int.TryParse(worksheet.Cells[row, 4].Text, out int chapterNumber))
                            continue;

                        DateTime? createdDate = null;
                        if (DateTime.TryParse(worksheet.Cells[row, 5].Text, out var dt))
                            createdDate = dt;

                        var title = worksheet.Cells[row, 1].Text?.Trim();
                        if (string.IsNullOrEmpty(title))
                            continue;

                        if (chapterList.Any(x => x.StoryId == storyId && x.ChapterNumber == chapterNumber))
                            continue;

                        chapterList.Add(new TblChapter
                        {
                            Title = title,
                            StoryId = storyId,
                            ChapterNumber = chapterNumber,
                            CreatedDate = createdDate
                        });
                    }


                    if (chapterList.Count > 0)
                    {
                        _context.TblChapters.AddRange(chapterList);
                        await _context.SaveChangesAsync();
                        return Json(new { success = true, message = $"Imported {chapterList.Count} chapters successfully!" });
                    }
                    
                    return Json(new { success = false, message = "No valid data was found in the file." });
                }
            }
        }
        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> SaveChapter(
    [FromForm] ChaptersViewModel model,
    [FromForm] string? TitleVi,
    [FromForm] string? TitleEn)
        {
            var isNew = model.ChapterId == 0;
            TblChapter chapter;

            var cleanTitleEn = !string.IsNullOrWhiteSpace(TitleEn) ? TitleEn.Trim() : (!string.IsNullOrWhiteSpace(model.Title) ? model.Title.Trim() : "");
            var cleanTitleVi = !string.IsNullOrWhiteSpace(TitleVi) ? TitleVi.Trim() : "";

            if (isNew)
            {
                // 1. BẢNG GỐC TBLCHAPTER CHỈ LƯU TIẾNG ANH
                chapter = new TblChapter
                {
                    Title = cleanTitleEn,
                    StoryId = model.StoryId,
                    ChapterNumber = model.ChapterNumber,
                    CreatedDate = DateTime.Now
                };

                await _context.TblChapters.AddAsync(chapter);
                await _context.SaveChangesAsync();
            }
            else
            {
                chapter = await _context.TblChapters
                    .Include(c => c.TblChapterTranslations)
                    .FirstOrDefaultAsync(x => x.ChapterId == model.ChapterId);

                if (chapter == null) return Json(new { success = false, message = "Chapter not found" });

                // Cập nhật tiếng Anh vào bảng gốc
                if (!string.IsNullOrWhiteSpace(cleanTitleEn)) chapter.Title = cleanTitleEn;
                chapter.StoryId = model.StoryId;
                chapter.ChapterNumber = model.ChapterNumber;
            }

            // 2. CHỈ LƯU TIẾNG VIỆT VÀO BẢNG TBLCHAPTERTRANSLATION
            if (!string.IsNullOrWhiteSpace(cleanTitleVi))
            {
                var transVi = await _context.TblChapterTranslations
                    .FirstOrDefaultAsync(t => t.ChapterId == chapter.ChapterId && t.LanguageCode == "vi-VN");

                if (transVi == null)
                {
                    _context.TblChapterTranslations.Add(new TblChapterTranslation
                    {
                        ChapterId = chapter.ChapterId,
                        LanguageCode = "vi-VN",
                        Title = cleanTitleVi
                    });
                }
                else
                {
                    transVi.Title = cleanTitleVi;
                }
            }

            // Dọn dẹp bản ghi en-US nếu có lỡ lưu trước đây
            var oldEnTrans = await _context.TblChapterTranslations
                .Where(t => t.ChapterId == chapter.ChapterId && t.LanguageCode.StartsWith("en"))
                .ToListAsync();
            if (oldEnTrans.Any())
            {
                _context.TblChapterTranslations.RemoveRange(oldEnTrans);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = isNew ? "Chapter added successfully!" : "Chapter updated successfully!" });
        }

        // 5. Dịch tự động tiêu đề chương bằng AI
        [HttpPost]
        public async Task<IActionResult> TranslateWithAi(string text, string fromLang, string toLang, [FromServices] IAiTranslationService aiService)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Json(new { success = false, message = "Original content is empty!" });

            try
            {
                var translated = await aiService.TranslateAsync(text, fromLang, toLang);
                if (string.IsNullOrWhiteSpace(translated))
                {
                    return Json(new { success = false, message = "AI did not return any translation!" });
                }
                return Json(new { success = true, result = translated });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}
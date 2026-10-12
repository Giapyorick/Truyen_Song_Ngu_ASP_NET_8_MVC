using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.Services;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize]
    public class tblParagraphsController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly TruyenSongNguContext _context;

        public tblParagraphsController(TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Paragraphs() => View();

        [HttpGet]
        public async Task<IActionResult> GetStoriesForSelect(string? culture)
        {
            var rqCulture = HttpContext.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>();
            var rawCulture = !string.IsNullOrWhiteSpace(culture)
                ? culture.Trim()
                : (rqCulture?.RequestCulture.UICulture.Name
                   ?? Request.Cookies[Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName]
                   ?? System.Globalization.CultureInfo.CurrentUICulture.Name);

            bool isVi = rawCulture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

            var list = await _context.TblStories
                .AsNoTracking()
                .Select(x => new
                {
                    id = x.StoryId,
                    name = isVi
                        ? (x.TblStoryTranslations
                            .Where(t => t.LanguageCode.StartsWith("vi"))
                            .Select(t => t.Title)
                            .FirstOrDefault() ?? x.Title)
                        : (x.TblStoryTranslations
                            .Where(t => t.LanguageCode.StartsWith("en"))
                            .Select(t => t.Title)
                            .FirstOrDefault() ?? x.Title)
                })
                .OrderBy(x => x.name)
                .ToListAsync();

            return Json(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetChaptersForSelect(int? id, int? storyId, string? culture)
        {
            int targetStoryId = id ?? storyId ?? 0;
            if (targetStoryId <= 0)
            {
                return Json(new List<object>());
            }

            var rqCulture = HttpContext.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>();
            var rawCulture = !string.IsNullOrWhiteSpace(culture)
                ? culture.Trim()
                : (rqCulture?.RequestCulture.UICulture.Name
                   ?? Request.Cookies[Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName]
                   ?? System.Globalization.CultureInfo.CurrentUICulture.Name);

            bool isVi = rawCulture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

            var list = await _context.TblChapters
                .AsNoTracking()
                .Where(x => x.StoryId == targetStoryId)
                .OrderBy(x => x.ChapterNumber)
                .Select(x => new
                {
                    id = x.ChapterId,
                    name = isVi
                        ? (x.TblChapterTranslations
                            .Where(ct => ct.LanguageCode.StartsWith("vi"))
                            .Select(ct => ct.Title)
                            .FirstOrDefault() ?? x.Title)
                        : (x.TblChapterTranslations
                            .Where(ct => ct.LanguageCode.StartsWith("en"))
                            .Select(ct => ct.Title)
                            .FirstOrDefault() ?? x.Title),
                    chapterNumber = x.ChapterNumber
                })
                .ToListAsync();

            return Json(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetChapterForEditor(int id)
        {
            var chapter = await _context.TblChapters
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.ChapterId == id);

            if (chapter == null)
            {
                return NotFound(new { success = false, message = "Chapter does not exist." });
            }

            var paragraphs = await _context.TblParagraphs
                .Where(p => p.ChapterId == id)
                .OrderBy(p => p.ParagraphOrder)
                .Select(p => new
                {
                    paragraphId = p.ParagraphId,
                    paragraphOrder = p.ParagraphOrder,
                    english = p.English,
                    vietnamese = p.Vietnamese,
                    chinese = p.Chinese,
                    japanese = p.Japanese,
                    french = p.French,
                    blockType = p.BlockType
                })
                .ToListAsync();

            return Json(new
            {
                chapterId = chapter.ChapterId,
                chapterTitle = chapter.Title,
                storyTitle = chapter.Story?.Title ?? "",
                paragraphs
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var para = await _context.TblParagraphs
                .AsNoTracking()
                .Where(p => p.ParagraphId == id)
                .Select(p => new
                {
                    p.ParagraphId,
                    p.ParagraphOrder,
                    p.Vietnamese,
                    p.English,
                    p.Chinese,
                    p.Japanese,
                    p.French,
                    p.BlockType,
                    Chapter = new
                    {
                        chapterId = p.ChapterId,
                        chapterTitle = p.Chap.Title,
                        storyId = p.Chap.StoryId
                    }
                })
                .FirstOrDefaultAsync();

            if (para == null) return NotFound();
            return Json(para);
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableLanguages(int chapterId)
        {
            var chapter = await _context.TblChapters
                .AsNoTracking()
                .Include(c => c.Story)
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId);

            if (chapter?.Story == null)
            {
                return Json(new { success = false, message = "Chapter or story not found." });
            }

            var rawLangs = (chapter.Story.Lang ?? "Tiếng Anh, Tiếng Việt")
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();

            var langList = new List<object>();

            foreach (var raw in rawLangs)
            {
                string normalizedKey = NormalizeLangKey(raw);
                if (!string.IsNullOrEmpty(normalizedKey))
                {
                    langList.Add(new
                    {
                        code = normalizedKey,
                        rawCode = raw,
                        name = GetLangDisplayName(normalizedKey)
                    });
                }
            }

            return Json(new { success = true, languages = langList });
        }

        [HttpGet]
        public async Task<IActionResult> GetLanguagesByStory(int storyId)
        {
            var story = await _context.TblStories
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StoryId == storyId);

            if (story == null || string.IsNullOrWhiteSpace(story.Lang))
            {
                return Json(new { success = true, languages = new List<object>() });
            }

            var rawLangs = story.Lang
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();

            var langList = new List<object>();

            foreach (var raw in rawLangs)
            {
                string normalizedKey = NormalizeLangKey(raw);
                if (!string.IsNullOrEmpty(normalizedKey))
                {
                    langList.Add(new
                    {
                        code = normalizedKey,
                        rawCode = raw,
                        name = GetLangDisplayName(normalizedKey)
                    });
                }
            }

            return Json(new { success = true, languages = langList });
        }

        // Chuẩn hóa từ khóa ngôn ngữ về chuỗi tiếng Anh mặc định
        private static string NormalizeLangKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            string val = input.Trim().ToLower();

            if (val.Contains("việt") || val.Contains("viet") || val == "vi" || val == "vie")
                return "vietnamese";

            if (val.Contains("anh") || val.Contains("eng") || val == "en" || val == "us")
                return "english";

            if (val.Contains("trung") || val.Contains("hoa") || val.Contains("chin") || val == "zh" || val == "cn" || val == "zho")
                return "chinese";

            if (val.Contains("nhật") || val.Contains("nhat") || val.Contains("jap") || val == "ja" || val == "jp" || val == "jpn")
                return "japanese";

            if (val.Contains("pháp") || val.Contains("phap") || val.Contains("fren") || val == "fr" || val == "fra")
                return "french";

            return val;
        }

        // Lấy tên hiển thị kèm lá cờ của ngôn ngữ
        private static string GetLangDisplayName(string code) => code switch
        {
            "vietnamese" => "Vietnamese 🇻🇳",
            "english" => "English 🇺🇸",
            "chinese" => "Chinese 🇨🇳",
            "japanese" => "Japanese 🇯🇵",
            "french" => "French 🇫🇷",
            _ => code.ToUpper()
        };

        [HttpGet]
        public async Task<IActionResult> List(string? search, int? storyId, int? cstoryId, int? chapterId, int page = 1, int pageSize = 10)
        {
            try
            {
                int? filterStoryId = storyId ?? cstoryId;

                var query = _context.TblParagraphs
                    .AsNoTracking()
                    .Select(p => new
                    {
                        p.ParagraphId,
                        p.ParagraphOrder,
                        English = p.English ?? "",
                        Vietnamese = p.Vietnamese ?? "",
                        Chinese = p.Chinese ?? "",
                        Japanese = p.Japanese ?? "",
                        French = p.French ?? "",
                        BlockType = p.BlockType ?? 1,
                        ChapterId = p.ChapterId,
                        ChapterTitle = p.Chap != null ? p.Chap.Title : "N/A",
                        StoryId = (p.Chap != null && p.Chap.Story != null) ? (int?)p.Chap.StoryId : null,
                        StoryTitle = (p.Chap != null && p.Chap.Story != null) ? p.Chap.Story.Title : "N/A"
                    })
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(x =>
                        x.English.Contains(search) ||
                        x.Vietnamese.Contains(search) ||
                        x.Chinese.Contains(search) ||
                        x.Japanese.Contains(search) ||
                        x.French.Contains(search)
                    );
                }

                if (filterStoryId.HasValue && filterStoryId.Value > 0)
                {
                    query = query.Where(x => x.StoryId == filterStoryId.Value);
                }

                if (chapterId.HasValue && chapterId.Value > 0)
                {
                    query = query.Where(x => x.ChapterId == chapterId.Value);
                }

                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                var data = await query
                    .OrderByDescending(u => u.StoryId)
                    .ThenBy(x => x.ChapterId)
                    .ThenBy(x => x.ParagraphOrder)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Json(new
                {
                    paragraphs = data,
                    currentPage = page,
                    totalPages,
                    totalItems
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> InsertAfter([FromBody] InsertParagraphViewModel model)
        {
            if (model == null || model.ChapterId <= 0)
            {
                return Json(new { success = false, message = "Invalid parameters provided." });
            }

            string Clean(string? text) => NormalizeQuillHtml(text) ?? "";
            string enText = Clean(model.English);
            string vnText = Clean(model.Vietnamese);

            if (string.IsNullOrWhiteSpace(enText) || string.IsNullOrWhiteSpace(vnText))
            {
                return Json(new { success = false, message = "English and Vietnamese content cannot be empty!" });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC sp_PrepareInsertAndReindexParagraph @ChapID = {model.ChapterId}, @TargetOrder = {model.TargetOrder}");

                var newParagraph = new TblParagraph
                {
                    ChapterId = model.ChapterId,
                    ParagraphOrder = model.TargetOrder + 1,
                    BlockType = model.BlockType ?? 1,
                    English = enText,
                    Vietnamese = vnText,
                    Chinese = Clean(model.Chinese),
                    Japanese = Clean(model.Japanese),
                    French = Clean(model.French),
                };

                _context.ChangeTracker.Clear();
                await _context.TblParagraphs.AddAsync(newParagraph);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = $"Inserted new paragraph at position #{newParagraph.ParagraphOrder} successfully!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Database error: {msg}" });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Update(ParagraphsViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid data payload." });

            var paragraph = await _context.TblParagraphs.FirstOrDefaultAsync(x => x.ParagraphId == model.ParagraphId);
            if (paragraph == null)
                return Json(new { success = false, message = "Paragraph not found." });

            paragraph.English = NormalizeQuillHtml(model.English) ?? "";
            paragraph.Vietnamese = NormalizeQuillHtml(model.Vietnamese) ?? "";
            paragraph.Chinese = NormalizeQuillHtml(model.Chinese);
            paragraph.Japanese = NormalizeQuillHtml(model.Japanese);
            paragraph.French = NormalizeQuillHtml(model.French);
            paragraph.BlockType = model.BlockType ?? 1;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Updated paragraph successfully." });
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> UpdateMultipleFromEditor([FromBody] List<ParagraphsViewModel> models)
        {
            if (models == null || !models.Any())
                return Json(new { success = false, message = "No data provided for update." });

            var ids = models.Select(m => m.ParagraphId).ToList();
            var paragraphs = await _context.TblParagraphs.Where(p => ids.Contains(p.ParagraphId)).ToListAsync();

            foreach (var para in paragraphs)
            {
                var model = models.FirstOrDefault(m => m.ParagraphId == para.ParagraphId);
                if (model != null)
                {
                    para.English = NormalizeQuillHtml(model.English) ?? "";
                    para.Vietnamese = NormalizeQuillHtml(model.Vietnamese) ?? "";
                    para.Chinese = NormalizeQuillHtml(model.Chinese);
                    para.Japanese = NormalizeQuillHtml(model.Japanese);
                    para.French = NormalizeQuillHtml(model.French);
                    para.BlockType = model.BlockType ?? 1;
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Updated all paragraphs successfully!" });
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Delete(int id)
        {
            var paragraph = await _context.TblParagraphs.FindAsync(id);
            if (paragraph == null)
                return Json(new { success = false, message = "Paragraph not found." });

            int chapterId = paragraph.ChapterId;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.TblParagraphs.Remove(paragraph);
                await _context.SaveChangesAsync();

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC sp_ReindexParagraphOrder @ChapID = {chapterId}");

                await transaction.CommitAsync();
                return Json(new { success = true, message = "Deleted and reindexed paragraph order successfully." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Delete failed: {innerError}" });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No paragraphs selected for deletion." });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var paragraphs = await _context.TblParagraphs.Where(x => ids.Contains(x.ParagraphId)).ToListAsync();
                if (!paragraphs.Any())
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "No valid paragraphs found." });
                }

                var affectedChapterIds = paragraphs.Select(x => x.ChapterId).Distinct().ToList();

                _context.TblParagraphs.RemoveRange(paragraphs);
                await _context.SaveChangesAsync();

                foreach (var chapId in affectedChapterIds)
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"EXEC sp_ReindexParagraphOrder @ChapID = {chapId}");
                }

                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Successfully deleted {paragraphs.Count} paragraphs!", deleted = paragraphs.Select(x => x.ParagraphId) });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Delete failed: {innerError}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string? search, int? chapterId)
        {
            var query = _context.TblParagraphs.AsNoTracking().AsQueryable();

            if (chapterId.HasValue && chapterId.Value > 0)
                query = query.Where(p => p.ChapterId == chapterId.Value);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.English.Contains(search) || p.Vietnamese.Contains(search));

            var data = await query
                .OrderBy(p => p.ParagraphOrder)
                .Select(p => new
                {
                    p.ParagraphId,
                    StoryTitle = p.Chap != null && p.Chap.Story != null ? p.Chap.Story.Title : "N/A",
                    ChapterTitle = p.Chap != null ? p.Chap.Title : "N/A",
                    p.ParagraphOrder,
                    p.English,
                    p.Vietnamese,
                    p.Chinese,
                    p.Japanese,
                    p.French,
                    BlockType = p.BlockType ?? 1
                })
                .ToListAsync();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Paragraphs");

            ws.Cells[1, 1].Value = "LIST OF PARAGRAPHS";
            ws.Cells[1, 1, 1, 10].Merge = true;
            ws.Cells[1, 1].Style.Font.Size = 16;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            string[] headers = { "Id", "Story", "Chapter", "Order", "English", "Vietnamese", "Chinese", "Japanese", "French", "BlockType" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[3, i + 1].Value = headers[i];
                ws.Cells[3, i + 1].Style.Font.Bold = true;
            }

            int row = 4;
            foreach (var p in data)
            {
                ws.Cells[row, 1].Value = p.ParagraphId;
                ws.Cells[row, 2].Value = p.StoryTitle;
                ws.Cells[row, 3].Value = p.ChapterTitle;
                ws.Cells[row, 4].Value = p.ParagraphOrder;
                ws.Cells[row, 5].Value = p.English;
                ws.Cells[row, 6].Value = p.Vietnamese;
                ws.Cells[row, 7].Value = p.Chinese;
                ws.Cells[row, 8].Value = p.Japanese;
                ws.Cells[row, 9].Value = p.French;
                ws.Cells[row, 10].Value = p.BlockType;
                row++;
            }

            ws.Cells.AutoFitColumns();
            return File(await package.GetAsByteArrayAsync(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Paragraphs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [AdminRoleAuthorize]
        public async Task<IActionResult> UploadIllustration(IFormFile file, [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Please select a valid image file!" });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return Json(new { success = false, message = "Only jpg, jpeg, png, webp, gif image files are allowed!" });
            }

            try
            {
                var uploadFolder = Path.Combine(env.WebRootPath, "assets", "image", "stories");
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                var fileName = $"{Guid.NewGuid():N}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                var fullPath = Path.Combine(uploadFolder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativeUrl = $"/assets/image/stories/{fileName}";

                return Json(new
                {
                    success = true,
                    url = relativeUrl,
                    message = "Uploaded image successfully!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Upload error: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult CheckJobStatus(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                return Json(new { completed = true });

            var monitoringApi = JobStorage.Current.GetMonitoringApi();
            var jobDetails = monitoringApi.JobDetails(jobId);

            if (jobDetails == null)
            {
                return Json(new { completed = true, state = "NotFoundOrFinished" });
            }

            var currentState = jobDetails.History.FirstOrDefault()?.StateName;
            bool isCompleted = currentState == "Succeeded" || currentState == "Failed" || currentState == "Deleted";

            return Json(new
            {
                completed = isCompleted,
                state = currentState
            });
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> EnqueueImportAi(int chapterId, IFormFile? fileEnglish, string? serverFileNameEnglish, string targetLanguages = "Vietnamese")
        {
            // Đọc nội dung file từ máy tính HOẶC server storage
            string rawEnText = ReadRawTextFromFileOrServer(fileEnglish, serverFileNameEnglish);

            if (chapterId <= 0 || string.IsNullOrWhiteSpace(rawEnText))
            {
                return Json(new { success = false, message = "Please select a valid Chapter and English source file!" });
            }

            try
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "temp_uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                // Đặt tên file tạm thời
                string fileName = fileEnglish != null ? fileEnglish.FileName : Path.GetFileName(serverFileNameEnglish ?? "en_source.txt");
                var tempFilePath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{fileName}");

                // Ghi dữ liệu ra temp file để Hangfire job đọc
                await System.IO.File.WriteAllTextAsync(tempFilePath, rawEnText, Encoding.UTF8);

                var jobId = BackgroundJob.Enqueue<IParagraphAiProcessingService>(service =>
                    service.ProcessChapterFileJob(chapterId, tempFilePath, targetLanguages));

                return Json(new
                {
                    success = true,
                    jobId,
                    message = "Sent to Hangfire background queue! System is automatically marking BlockType and translating."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"AI process creation error: {ex.Message}" });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> ImportFromFilesDirect(
    int chapterId,
    IFormFile? fileEnglish,
    string? serverFileNameEnglish,
    IFormFile? fileVietnamese,
    string? serverFileNameVietnamese,
    IFormFile? fileChinese,
    string? serverFileNameChinese,
    IFormFile? fileJapanese,
    string? serverFileNameJapanese,
    IFormFile? fileFrench,
    string? serverFileNameFrench)
        {
            // Đọc nội dung file Tiếng Anh nguồn
            string rawEnText = ReadRawTextFromFileOrServer(fileEnglish, serverFileNameEnglish);

            if (chapterId <= 0 || string.IsNullOrWhiteSpace(rawEnText))
            {
                return Json(new { success = false, message = "Please select a Chapter and provide a valid English source file!" });
            }

            try
            {
                var enBlocks = SegmentRawTextToBlocks(rawEnText);

                if (enBlocks.Count == 0)
                {
                    return Json(new { success = false, message = "The English source file does not contain any valid sentences!" });
                }

                string rawViText = ReadRawTextFromFileOrServer(fileVietnamese, serverFileNameVietnamese);
                string rawZhText = ReadRawTextFromFileOrServer(fileChinese, serverFileNameChinese);
                string rawJaText = ReadRawTextFromFileOrServer(fileJapanese, serverFileNameJapanese);
                string rawFrText = ReadRawTextFromFileOrServer(fileFrench, serverFileNameFrench);

                var viBlocks = !string.IsNullOrWhiteSpace(rawViText) ? SegmentRawTextToBlocks(rawViText) : null;
                var zhBlocks = !string.IsNullOrWhiteSpace(rawZhText) ? SegmentRawTextToBlocks(rawZhText) : null;
                var jaBlocks = !string.IsNullOrWhiteSpace(rawJaText) ? SegmentRawTextToBlocks(rawJaText) : null;
                var frBlocks = !string.IsNullOrWhiteSpace(rawFrText) ? SegmentRawTextToBlocks(rawFrText) : null;

                var mismatchedLangs = new List<string>();

                if (viBlocks != null && viBlocks.Count != enBlocks.Count)
                    mismatchedLangs.Add($"Vietnamese ({viBlocks.Count}/{enBlocks.Count} sentences)");

                if (zhBlocks != null && zhBlocks.Count != enBlocks.Count)
                    mismatchedLangs.Add($"Chinese ({zhBlocks.Count}/{enBlocks.Count} sentences)");

                if (jaBlocks != null && jaBlocks.Count != enBlocks.Count)
                    mismatchedLangs.Add($"Japanese ({jaBlocks.Count}/{enBlocks.Count} sentences)");

                if (frBlocks != null && frBlocks.Count != enBlocks.Count)
                    mismatchedLangs.Add($"French ({frBlocks.Count}/{enBlocks.Count} sentences)");

                if (mismatchedLangs.Count > 0)
                {
                    string details = string.Join(", ", mismatchedLangs);
                    return Json(new
                    {
                        success = false,
                        message = $"Sentence count mismatch compared to English source ({enBlocks.Count} sentences)! Details: {details}."
                    });
                }

                // TÍNH TOÁN THỨ TỰ TỰ ĐỘNG NỐI TIẾP (APPEND)
                int currentMaxOrder = await _context.TblParagraphs
                    .Where(p => p.ChapterId == chapterId)
                    .Select(p => (int?)p.ParagraphOrder)
                    .MaxAsync() ?? -1;

                int orderTracker = currentMaxOrder >= 0 ? currentMaxOrder + 1 : 0;
                var newEntities = new List<TblParagraph>();

                for (int i = 0; i < enBlocks.Count; i++)
                {
                    var enBlock = enBlocks[i];

                    string viText = viBlocks != null ? viBlocks[i].English : "";
                    string zhText = zhBlocks != null ? zhBlocks[i].English : "";
                    string jaText = jaBlocks != null ? jaBlocks[i].English : "";
                    string frText = frBlocks != null ? frBlocks[i].English : "";

                    int finalBlockType = enBlock.BlockType;

                    // CHỈ gán BlockType = 0 (mở đầu chương) khi chương NÀY CHƯA CÓ ĐOẠN NÀO
                    if (i == 0 && currentMaxOrder == -1)
                    {
                        finalBlockType = 0;
                    }

                    newEntities.Add(new TblParagraph
                    {
                        ChapterId = chapterId,
                        ParagraphOrder = orderTracker++,
                        BlockType = finalBlockType,
                        English = enBlock.English.Trim(),
                        Vietnamese = CleanLeadingDash(viText),
                        Chinese = string.IsNullOrWhiteSpace(zhText) ? null : CleanLeadingDash(zhText),
                        Japanese = string.IsNullOrWhiteSpace(jaText) ? null : CleanLeadingDash(jaText),
                        French = string.IsNullOrWhiteSpace(frText) ? null : CleanLeadingDash(frText)
                    });
                }

                await _context.TblParagraphs.AddRangeAsync(newEntities);
                await _context.SaveChangesAsync();

                // Đánh lại số thứ tự chuẩn hóa
                await _context.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_ReindexParagraphOrder @ChapID = {chapterId}");

                return Json(new
                {
                    success = true,
                    message = $"Successfully appended {newEntities.Count} paragraphs to chapter!"
                });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"File processing error: {msg}" });
            }
        }
        // Hàm đọc nội dung đa năng (Hỗ trợ file FormFile upload trực tiếp HOẶC file từ Kho lưu trữ/assets/template)
        private string ReadRawTextFromFileOrServer(IFormFile? file, string? serverFileName)
        {
            if (file != null && file.Length > 0)
            {
                return ReadRawTextFromFormFile(file);
            }

            if (!string.IsNullOrWhiteSpace(serverFileName))
            {
                string decodedFileName = System.Net.WebUtility.UrlDecode(serverFileName);
                string fileNameOnly = Path.GetFileName(decodedFileName);

                var possiblePaths = new[]
                {
                    Path.Combine(_webHostEnvironment.WebRootPath, "admin_storage", fileNameOnly),
                    Path.Combine(_webHostEnvironment.WebRootPath, "assets", "template", fileNameOnly),
                    Path.Combine(_webHostEnvironment.WebRootPath, "temp_uploads", fileNameOnly),
                    Path.Combine(_webHostEnvironment.WebRootPath, "assets", "uploads", fileNameOnly),
                    Path.Combine(_webHostEnvironment.WebRootPath, fileNameOnly)
                };

                foreach (var path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        string ext = Path.GetExtension(path).ToLower();
                        if (ext == ".txt")
                        {
                            return System.IO.File.ReadAllText(path, Encoding.UTF8);
                        }

                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                        using var streamEx = System.IO.File.OpenRead(path);
                        using var package = new ExcelPackage(streamEx);
                        if (package.Workbook.Worksheets.Count > 0)
                        {
                            var sheet = package.Workbook.Worksheets[0];
                            int rowCount = sheet.Dimension?.Rows ?? 0;
                            var sb = new StringBuilder();
                            for (int row = 1; row <= rowCount; row++)
                            {
                                string cell = sheet.Cells[row, 1].Text;
                                if (!string.IsNullOrWhiteSpace(cell))
                                {
                                    sb.AppendLine(cell.Trim());
                                }
                            }
                            return sb.ToString();
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static string CleanLeadingDash(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return Regex.Replace(text.Trim(), @"^[–-]\s*", "").Trim();
        }

        private static string ReadRawTextFromFormFile(IFormFile file)
        {
            string ext = Path.GetExtension(file.FileName).ToLower();
            var sb = new StringBuilder();

            if (ext == ".txt")
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var streamEx = file.OpenReadStream();
            using var package = new ExcelPackage(streamEx);
            if (package.Workbook.Worksheets.Count > 0)
            {
                var sheet = package.Workbook.Worksheets[0];
                int rowCount = sheet.Dimension?.Rows ?? 0;
                for (int row = 1; row <= rowCount; row++)
                {
                    string cell = sheet.Cells[row, 1].Text;
                    if (!string.IsNullOrWhiteSpace(cell))
                    {
                        sb.AppendLine(cell.Trim());
                    }
                }
            }
            return sb.ToString();
        }

        private class ParsedBlockInfo
        {
            public int BlockType { get; set; } = 1;
            public string English { get; set; } = "";
        }

        private static List<ParsedBlockInfo> SegmentRawTextToBlocks(string rawText)
        {
            var result = new List<ParsedBlockInfo>();
            if (string.IsNullOrWhiteSpace(rawText)) return result;

            rawText = rawText.Replace("\r\n", "\n").Replace("\r", "\n");
            var rawParagraphs = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            bool isFirstParagraphInChapter = true;

            foreach (var para in rawParagraphs)
            {
                if (string.IsNullOrWhiteSpace(para)) continue;

                var sentences = Regex.Split(para, @"(?<=[\.!\?。！？][”""」』]?)\s*(?=[A-Z0-9“""ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯĂẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼỀỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴÝỶỸ–-]|[\u4e00-\u9fa5\u3040-\u30ff])")
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                bool isFirstInParagraph = true;

                foreach (var sentence in sentences)
                {
                    string cleanSentence = sentence;
                    int blockType = 1;

                    bool startsWithDash = cleanSentence.StartsWith("-") || cleanSentence.StartsWith("–");

                    if (startsWithDash)
                    {
                        blockType = 4;
                        cleanSentence = Regex.Replace(cleanSentence, @"^[–-]\s*", "").Trim();
                    }
                    else if (isFirstParagraphInChapter)
                    {
                        blockType = 0;
                        isFirstParagraphInChapter = false;
                    }
                    else if (Regex.IsMatch(cleanSentence, @"[""“][^""”]+[""”]"))
                    {
                        blockType = 4;
                    }
                    else if (isFirstInParagraph)
                    {
                        blockType = 2;
                    }

                    isFirstInParagraph = false;

                    if (!string.IsNullOrWhiteSpace(cleanSentence))
                    {
                        result.Add(new ParsedBlockInfo
                        {
                            BlockType = blockType,
                            English = cleanSentence
                        });
                    }
                }
            }

            return result;
        }

        private static string? NormalizeQuillHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return null;
            html = html.Trim();
            if (html == "<p><br></p>" || html == "<p></p>") return null;
            if (html.StartsWith("<p>") && html.EndsWith("</p>"))
            {
                var inner = html.Substring(3, html.Length - 7).Trim();
                if (!inner.Contains("</p>")) return inner;
            }

            string clean = Regex.Replace(html, @"<\/?p[^>]*>", "");
            return Regex.Replace(clean, @"<br\s*\/?>", " ").Trim();
        }

        public class MergeParagraphRequest
        {
            public int TargetParagraphId { get; set; }
            public string Direction { get; set; } = "prev";
            public bool RemoveImages { get; set; } = true;
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> AnalyzeImportFiles(
            IFormFile fileEnglish,
            IFormFile? fileVietnamese,
            IFormFile? fileChinese,
            IFormFile? fileJapanese,
            IFormFile? fileFrench)
        {
            if (fileEnglish == null || fileEnglish.Length == 0)
            {
                return Json(new { success = false, message = "Please provide an English source file!" });
            }

            try
            {
                var enBlocks = SegmentRawTextToBlocks(ReadRawTextFromFormFile(fileEnglish));
                int enCount = enBlocks.Count;

                var result = new Dictionary<string, int> { { "English", enCount } };
                var mismatched = new List<string>();

                void CheckLang(string langName, IFormFile? file)
                {
                    if (file != null && file.Length > 0)
                    {
                        var blocks = SegmentRawTextToBlocks(ReadRawTextFromFormFile(file));
                        result[langName] = blocks.Count;
                        if (blocks.Count != enCount)
                        {
                            mismatched.Add($"{langName} ({blocks.Count}/{enCount} sentences)");
                        }
                    }
                }

                CheckLang("Vietnamese", fileVietnamese);
                CheckLang("Chinese", fileChinese);
                CheckLang("Japanese", fileJapanese);
                CheckLang("French", fileFrench);

                bool isMismatched = mismatched.Count > 0;

                return Json(new
                {
                    success = true,
                    isMismatched,
                    counts = result,
                    mismatchedDetails = string.Join(", ", mismatched),
                    message = isMismatched
                        ? $"Sentence count mismatch detected across files! Details: {string.Join(", ", mismatched)}."
                        : $"All files matched perfectly with {enCount} sentences!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"File analysis error: {ex.Message}" });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> AiAutoAlignAndImport(
            int chapterId,
            IFormFile fileEnglish,
            IFormFile? fileVietnamese,
            IFormFile? fileChinese,
            IFormFile? fileJapanese,
            IFormFile? fileFrench)
        {
            if (chapterId <= 0 || fileEnglish == null || fileEnglish.Length == 0)
            {
                return Json(new { success = false, message = "Please select a Chapter and a valid English source file!" });
            }

            try
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "temp_uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                async Task<string?> SaveTempFileAsync(IFormFile? file)
                {
                    if (file == null || file.Length == 0) return null;
                    var path = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{file.FileName}");
                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    return path;
                }

                // Lưu các file thực sự được upload
                string? tempEn = await SaveTempFileAsync(fileEnglish);
                string? tempVi = await SaveTempFileAsync(fileVietnamese);
                string? tempZh = await SaveTempFileAsync(fileChinese);
                string? tempJa = await SaveTempFileAsync(fileJapanese);
                string? tempFr = await SaveTempFileAsync(fileFrench);

                if (string.IsNullOrEmpty(tempEn))
                {
                    return Json(new { success = false, message = "Could not save temporary English source file!" });
                }

                // Đẩy tác vụ vào Hangfire queue
                var jobId = BackgroundJob.Enqueue<IParagraphAiProcessingService>(service =>
                    service.ProcessAiAlignAndImportJob(chapterId, tempEn, tempVi, tempZh, tempJa, tempFr));

                return Json(new
                {
                    success = true,
                    jobId,
                    message = "Successfully submitted AI auto-alignment task to Hangfire background process!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hangfire process creation error: {ex.Message}" });
            }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> MergeParagraphs([FromBody] MergeParagraphRequest request)
        {
            if (request == null || request.TargetParagraphId <= 0)
                return Json(new { success = false, message = "Invalid data provided." });

            var target = await _context.TblParagraphs.FirstOrDefaultAsync(p => p.ParagraphId == request.TargetParagraphId);
            if (target == null)
                return Json(new { success = false, message = "Specified paragraph not found." });

            int chapterId = target.ChapterId;

            TblParagraph? other = null;
            if (request.Direction.ToLower() == "prev")
            {
                other = await _context.TblParagraphs
                    .Where(p => p.ChapterId == chapterId && p.ParagraphOrder < target.ParagraphOrder)
                    .OrderByDescending(p => p.ParagraphOrder)
                    .FirstOrDefaultAsync();
            }
            else
            {
                other = await _context.TblParagraphs
                    .Where(p => p.ChapterId == chapterId && p.ParagraphOrder > target.ParagraphOrder)
                    .OrderBy(p => p.ParagraphOrder)
                    .FirstOrDefaultAsync();
            }

            if (other == null)
            {
                string dirText = request.Direction.ToLower() == "prev" ? "previous" : "next";
                return Json(new { success = false, message = $"No {dirText} paragraph found to merge with!" });
            }

            TblParagraph first = target.ParagraphOrder < other.ParagraphOrder ? target : other;
            TblParagraph second = target.ParagraphOrder < other.ParagraphOrder ? other : target;

            string CombineText(string? text1, string? text2)
            {
                string t1 = text1 ?? "";
                string t2 = text2 ?? "";

                if (request.RemoveImages)
                {
                    t1 = Regex.Replace(t1, @"\[img[\s\S]*?\]", "", RegexOptions.IgnoreCase).Trim();
                    t2 = Regex.Replace(t2, @"\[img[\s\S]*?\]", "", RegexOptions.IgnoreCase).Trim();
                }

                if (string.IsNullOrWhiteSpace(t1)) return t2.Trim();
                if (string.IsNullOrWhiteSpace(t2)) return t1.Trim();
                return $"{t1.Trim()} {t2.Trim()}";
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                first.English = CombineText(first.English, second.English);
                first.Vietnamese = CombineText(first.Vietnamese, second.Vietnamese);
                first.Chinese = CombineText(first.Chinese, second.Chinese);
                first.Japanese = CombineText(first.Japanese, second.Japanese);
                first.French = CombineText(first.French, second.French);

                _context.TblParagraphs.Remove(second);
                await _context.SaveChangesAsync();

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC [dbo].[sp_ReindexParagraphOrder] @ChapID = {chapterId}");

                await transaction.CommitAsync();

                return Json(new { success = true, message = "Successfully merged paragraphs and reindexed order!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Error merging paragraphs: {msg}" });
            }
        }
    }
}
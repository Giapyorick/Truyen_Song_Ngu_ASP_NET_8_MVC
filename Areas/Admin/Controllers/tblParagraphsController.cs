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
        private readonly IAiCompletionService _ai;

        public tblParagraphsController(TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment, IAiCompletionService ai)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _ai = ai;
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

        // =====================================================================
        // IMPORT CÂU/ĐOẠN (file theo ngôn ngữ, Excel nhiều cột, AI dịch / căn chỉnh)
        // Đọc file + tách câu: Helpers/ParagraphImportParser.cs
        // Job AI (Hangfire):    Helpers/ParagraphAiProcessingService.cs
        // =====================================================================

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

            var lastState = jobDetails.History.FirstOrDefault();
            var currentState = lastState?.StateName;
            bool isCompleted = currentState == "Succeeded" || currentState == "Failed" || currentState == "Deleted";

            // Job lỗi: lấy thông báo lỗi Hangfire đã lưu để hiện cho admin (thay vì bảo "xem log Hangfire")
            string? message = null;
            if (currentState == "Failed" && lastState?.Data != null &&
                lastState.Data.TryGetValue("ExceptionMessage", out var exMsg))
            {
                message = exMsg;
            }

            return Json(new
            {
                completed = isCompleted,
                state = currentState,
                message
            });
        }

        /// <summary>Danh sách model AI cho ô chọn ở modal Import (model chưa có API key bị khóa)</summary>
        [HttpGet]
        public IActionResult GetAiModels()
        {
            var def = _ai.DefaultModel;
            return Json(new
            {
                success = true,
                defaultModel = def.Id,
                models = _ai.Models.Select(m => new
                {
                    id = m.Id,
                    provider = m.Provider,
                    name = m.Name,
                    note = m.Note,
                    configured = _ai.IsConfigured(m.Provider)
                })
            });
        }

        /// <summary>Import có AI dịch (các ngôn ngữ còn lại lấy từ file / cột Excel)</summary>
        [HttpPost]
        [AdminRoleAuthorize]
        public Task<IActionResult> EnqueueImportAi(int chapterId, string? targetLanguages)
            => RunImportAsync(chapterId, targetLanguages, allowAlign: false);

        /// <summary>Import trực tiếp từ file (không AI)</summary>
        [HttpPost]
        [AdminRoleAuthorize]
        public Task<IActionResult> ImportFromFilesDirect(int chapterId)
            => RunImportAsync(chapterId, null, allowAlign: false);

        /// <summary>Import có AI căn chỉnh bản dịch lệch số câu (+ AI dịch nếu có chọn)</summary>
        [HttpPost]
        [AdminRoleAuthorize]
        public Task<IActionResult> AiAutoAlignAndImport(int chapterId, string? targetLanguages)
            => RunImportAsync(chapterId, targetLanguages, allowAlign: true);

        /// <summary>
        /// Phân tích số câu của các file (không ghi gì). Dùng chung dữ liệu form với nút Import.
        /// </summary>
        [HttpPost]
        [AdminRoleAuthorize]
        public IActionResult AnalyzeImportFiles(string? targetLanguages)
        {
            try
            {
                var build = BuildImportPayload(0, ParseLanguageList(targetLanguages), allowAlign: true);
                if (build.Error != null) return Json(new { success = false, message = build.Error });

                bool isMismatched = build.Mismatched.Count > 0;
                string details = string.Join(", ", build.Mismatched);
                return Json(new
                {
                    success = true,
                    isMismatched,
                    counts = build.Counts,
                    tableLanguages = build.TableLanguages,
                    mismatchedDetails = details,
                    message = isMismatched
                        ? $"Số câu bị lệch so với tiếng Anh ({build.Payload!.English.Count} câu): {details}."
                        : $"Tất cả file khớp nhau: {build.Payload!.English.Count} câu."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi phân tích file: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xem nhanh file tiếng Anh nguồn: có phải Excel nhiều cột không, có sẵn cột ngôn ngữ nào
        /// (để giao diện đánh dấu các ngôn ngữ đó là "có sẵn trong file Excel").
        /// </summary>
        [HttpPost]
        [AdminRoleAuthorize]
        public IActionResult InspectImportFile()
        {
            try
            {
                var source = ReadImportSource("English");
                if (source == null) return Json(new { success = false, message = "Chưa có file tiếng Anh nguồn." });

                return Json(new
                {
                    success = true,
                    isTable = source.IsTable,
                    languages = source.TableLanguages.Select(LangDisplay).ToList(),
                    rows = source.IsTable ? source.Rows.Count : 0
                });
            }
            catch (ParagraphImportParser.ImportFileException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi đọc file: {ex.Message}" });
            }
        }

        // ---------------------------------------------------------------------
        // Xử lý chung cho 3 nút import
        // ---------------------------------------------------------------------
        private async Task<IActionResult> RunImportAsync(int chapterId, string? targetLanguages, bool allowAlign)
        {
            if (chapterId <= 0 || !await _context.TblChapters.AnyAsync(c => c.ChapterId == chapterId))
                return Json(new { success = false, message = "Vui lòng chọn chương hợp lệ!" });

            try
            {
                var aiLanguages = ParseLanguageList(targetLanguages);
                var build = BuildImportPayload(chapterId, aiLanguages, allowAlign);
                if (build.Error != null) return Json(new { success = false, message = build.Error });

                var payload = build.Payload!;

                // Model AI admin chọn (chỉ cần khi có AI dịch / căn chỉnh)
                string? aiModelId = Request.HasFormContentType ? Request.Form["aiModel"].ToString() : null;
                var aiModel = _ai.Find(aiModelId) ?? _ai.DefaultModel;   // mã lạ / bỏ trống -> model mặc định
                payload.AiModel = aiModel.Id;

                // Lệch số câu mà chưa cho phép AI căn chỉnh -> JS hỏi người dùng có muốn AI căn chỉnh không
                // (thông báo phải chứa chữ "lệch" để JS nhận ra)
                if (!allowAlign && build.Mismatched.Count > 0)
                {
                    return Json(new
                    {
                        success = false,
                        isMismatched = true,
                        message = $"Số câu bị lệch so với tiếng Anh ({payload.English.Count} câu): {string.Join(", ", build.Mismatched)}."
                    });
                }

                // Không cần AI -> ghi ngay
                if (!payload.NeedsAi)
                {
                    int saved = await ParagraphImportParser.SaveAsync(_context, chapterId, payload.English, payload.Provided);
                    return Json(new { success = true, message = $"Đã thêm {saved} câu vào chương!" });
                }

                // Cần AI -> kiểm tra API key của model đã chọn
                if (!_ai.IsConfigured(aiModel.Provider))
                    return Json(new { success = false, message = $"Chưa cấu hình API key cho {aiModel.Name}. Hãy chọn model khác hoặc thêm key ({(aiModel.Provider == AiCompletionService.Claude ? "Anthropic:ApiKey" : "Groq:ApiKey")})." });

                // Cần AI -> lưu dữ liệu ra App_Data rồi giao cho Hangfire
                string payloadPath = await ParagraphImportParser.SavePayloadAsync(payload, _webHostEnvironment.ContentRootPath);
                var jobId = BackgroundJob.Enqueue<IParagraphAiProcessingService>(service => service.RunImportJob(payloadPath));

                var tasks = new List<string>();
                if (payload.AiLanguages.Count > 0) tasks.Add("dịch " + string.Join(", ", payload.AiLanguages.Select(LangDisplay)));
                if (payload.ToAlign.Count > 0) tasks.Add("căn chỉnh " + string.Join(", ", payload.ToAlign.Keys.Select(LangDisplay)));

                return Json(new
                {
                    success = true,
                    jobId,
                    message = $"Đã gửi {payload.English.Count} câu cho {aiModel.Name} xử lý nền ({string.Join("; ", tasks)})."
                });
            }
            catch (ParagraphImportParser.ImportFileException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = $"Lỗi import: {msg}" });
            }
        }

        private class ImportBuildResult
        {
            public ParagraphImportParser.ImportPayload? Payload { get; set; }
            public string? Error { get; set; }
            public List<string> Mismatched { get; } = new();
            public Dictionary<string, int> Counts { get; } = new();
            public List<string> TableLanguages { get; set; } = new();
        }

        /// <summary>
        /// Đọc file tiếng Anh + các ngôn ngữ đích từ form (file tải lên hoặc serverFileName* từ kho lưu trữ),
        /// ghép thành payload. Ngôn ngữ chọn AI dịch -> AiLanguages; file khớp số câu -> Provided;
        /// file lệch -> ToAlign (nếu allowAlign) và luôn ghi vào Mismatched.
        /// Ngôn ngữ không có file nhưng có cột trong Excel tiếng Anh nhiều cột -> lấy từ cột đó.
        /// </summary>
        private ImportBuildResult BuildImportPayload(int chapterId, List<string> aiLanguages, bool allowAlign)
        {
            var result = new ImportBuildResult();

            var english = ReadImportSource("English");
            if (english == null || english.IsEmpty)
            {
                result.Error = "Vui lòng chọn file tiếng Anh nguồn (.xlsx hoặc .txt) có nội dung!";
                return result;
            }

            // Chỉ sửa lỗi OCR khi có AI dịch (giữ như hành vi cũ của chế độ AI)
            var enBlocks = ParagraphImportParser.EnglishBlocks(english, sanitizeOcr: aiLanguages.Count > 0);
            if (enBlocks.Count == 0)
            {
                result.Error = "File tiếng Anh nguồn không có câu nào hợp lệ!";
                return result;
            }

            var payload = new ParagraphImportParser.ImportPayload { ChapterId = chapterId, English = enBlocks };
            result.Payload = payload;
            result.Counts["English"] = enBlocks.Count;
            result.TableLanguages = english.TableLanguages.Select(LangDisplay).ToList();

            foreach (var lang in ParagraphImportParser.TargetLanguages)
            {
                if (aiLanguages.Contains(lang))
                {
                    payload.AiLanguages.Add(lang);
                    continue;
                }

                List<string>? lines = null;
                var source = ReadImportSource(LangDisplay(lang));
                if (source != null && !source.IsEmpty)
                {
                    lines = ParagraphImportParser.LanguageLines(source, lang);
                }
                else if (english.IsTable && english.TableLanguages.Contains(lang))
                {
                    // Excel tiếng Anh nhiều cột đã có sẵn cột ngôn ngữ này -> khớp 1-1 theo dòng
                    lines = ParagraphImportParser.LanguageLines(english, lang);
                }

                if (lines == null) continue;

                result.Counts[LangDisplay(lang)] = lines.Count;
                if (lines.Count == enBlocks.Count)
                {
                    payload.Provided[lang] = lines;
                }
                else
                {
                    result.Mismatched.Add($"{LangDisplay(lang)} ({lines.Count}/{enBlocks.Count} câu)");
                    if (allowAlign) payload.ToAlign[lang] = lines;
                }
            }

            return result;
        }

        /// <summary>
        /// Đọc file của 1 ngôn ngữ từ request: ưu tiên file tải lên (file{Lang}), sau đó file trong kho (serverFileName{Lang}).
        /// null = không có file.
        /// </summary>
        private ParagraphImportParser.SourceFile? ReadImportSource(string langName)
        {
            if (!Request.HasFormContentType) return null;

            var file = Request.Form.Files.GetFile("file" + langName);
            if (file != null && file.Length > 0)
            {
                using var stream = file.OpenReadStream();
                return ParagraphImportParser.Read(stream, file.FileName);
            }

            string? serverFileName = Request.Form["serverFileName" + langName];
            if (string.IsNullOrWhiteSpace(serverFileName)) return null;

            // Chỉ lấy tên file (chặn "../" trỏ ra ngoài thư mục cho phép)
            string fileNameOnly = Path.GetFileName(System.Net.WebUtility.UrlDecode(serverFileName));
            if (string.IsNullOrWhiteSpace(fileNameOnly)) return null;

            var possiblePaths = new[]
            {
                Path.Combine(_webHostEnvironment.WebRootPath, "assets", "template", fileNameOnly),
                Path.Combine(_webHostEnvironment.WebRootPath, "admin_storage", fileNameOnly),
                Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "admin_repository", fileNameOnly),
                Path.Combine(_webHostEnvironment.WebRootPath, "assets", "uploads", fileNameOnly)
            };

            var path = possiblePaths.FirstOrDefault(System.IO.File.Exists);
            if (path == null)
                throw new ParagraphImportParser.ImportFileException($"Không tìm thấy file \"{fileNameOnly}\" trong kho lưu trữ.");

            using var fs = System.IO.File.OpenRead(path);
            return ParagraphImportParser.Read(fs, fileNameOnly);
        }

        /// <summary>"Vietnamese,Chinese" -> ["vietnamese","chinese"] (bỏ English và tên lạ)</summary>
        private static List<string> ParseLanguageList(string? languages)
        {
            if (string.IsNullOrWhiteSpace(languages)) return new List<string>();
            return languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParagraphImportParser.NormalizeLanguage)
                .Where(l => l != null && l != ParagraphImportParser.English)
                .Select(l => l!)
                .Distinct()
                .ToList();
        }

        /// <summary>"vietnamese" -> "Vietnamese" (trùng tên field form: fileVietnamese, serverFileNameVietnamese)</summary>
        private static string LangDisplay(string lang) =>
            string.IsNullOrEmpty(lang) ? lang : char.ToUpperInvariant(lang[0]) + lang.Substring(1);

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
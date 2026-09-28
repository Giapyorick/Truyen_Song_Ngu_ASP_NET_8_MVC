using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebTruyenTranh.Models;
using WebTruyenTranh.Services;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
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
        public async Task<IActionResult> GetStoriesForSelect()
        {
            var list = await _context.TblStories
                .AsNoTracking()
                .Select(x => new { id = x.StoryId, name = x.Title })
                .ToListAsync();
            return Json(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetChaptersForSelect(int id)
        {
            var list = await _context.TblChapters
                .AsNoTracking()
                .Where(x => x.StoryId == id)
                .Select(x => new { id = x.ChapterId, name = x.Title })
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
                return NotFound(new { success = false, message = "Chapter không tồn tại." });
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

            // Tách chuỗi theo dấu phẩy, chấm phẩy hoặc gạch đứng
            var rawLangs = (chapter.Story.Lang ?? "Tiếng Anh, Tiếng Việt")
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();

            var langList = new List<object>();

            foreach (var raw in rawLangs)
            {
                // 1. Chuẩn hóa chuỗi CSDL ("Tiếng Trung", "zh", ...) về key chuẩn ("chinese")
                string normalizedKey = NormalizeLangKey(raw);

                if (!string.IsNullOrEmpty(normalizedKey))
                {
                    langList.Add(new
                    {
                        code = normalizedKey,                      // "english", "vietnamese", "chinese", "japanese", "french"
                        rawCode = raw,                             // Giữ nguyên chuỗi gốc
                        name = GetLangDisplayName(normalizedKey)   // Truyền normalizedKey để lấy đúng "Chinese 🇨🇳", "Japanese 🇯🇵"...
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
        private static string NormalizeLangKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            string val = input.Trim().ToLower();

            // Nhận diện Tiếng Việt
            if (val.Contains("việt") || val.Contains("Việt") || val.Contains("viet") || val == "vi" || val == "vie")
                return "vietnamese";

            // Nhận diện Tiếng Anh
            if (val.Contains("anh") || val.Contains("Anh") || val.Contains("eng") || val == "en" || val == "us")
                return "english";

            // Nhận diện Tiếng Trung
            if (val.Contains("trung") || val.Contains("Trung") || val.Contains("hoa") || val.Contains("chin") || val == "zh" || val == "cn" || val == "zho")
                return "chinese";

            // Nhận diện Tiếng Nhật
            if (val.Contains("nhật") || val.Contains("Nhật") || val.Contains("nhat") || val.Contains("jap") || val == "ja" || val == "jp" || val == "jpn")
                return "japanese";

            // Nhận diện Tiếng Pháp
            if (val.Contains("pháp") || val.Contains("Pháp") || val.Contains("phap") || val.Contains("fren") || val == "fr" || val == "fra")
                return "french";

            return val;
        }

        // Hiển thị nhãn UI tiếng Anh kèm cờ quốc gia
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
        public async Task<IActionResult> InsertAfter([FromBody] InsertParagraphViewModel model)
        {
            if (model == null || model.ChapterId <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            string Clean(string? text) => NormalizeQuillHtml(text) ?? "";
            string enText = Clean(model.English);
            string vnText = Clean(model.Vietnamese);

            if (string.IsNullOrWhiteSpace(enText) || string.IsNullOrWhiteSpace(vnText))
            {
                return Json(new { success = false, message = "Tiếng Anh và Tiếng Việt không được để trống!" });
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
                    message = $"Đã chèn đoạn mới vào vị trí #{newParagraph.ParagraphOrder} thành công!"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi CSDL: {msg}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Update(ParagraphsViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid data" });

            var paragraph = await _context.TblParagraphs.FirstOrDefaultAsync(x => x.ParagraphId == model.ParagraphId);
            if (paragraph == null)
                return Json(new { success = false, message = "Paragraph not found" });

            paragraph.English = NormalizeQuillHtml(model.English) ?? "";
            paragraph.Vietnamese = NormalizeQuillHtml(model.Vietnamese) ?? "";
            paragraph.Chinese = NormalizeQuillHtml(model.Chinese);
            paragraph.Japanese = NormalizeQuillHtml(model.Japanese);
            paragraph.French = NormalizeQuillHtml(model.French);
            paragraph.BlockType = model.BlockType ?? 1;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Updated successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMultipleFromEditor([FromBody] List<ParagraphsViewModel> models)
        {
            if (models == null || !models.Any())
                return Json(new { success = false, message = "Không có dữ liệu thay đổi." });

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
            return Json(new { success = true, message = "Đã cập nhật toàn bộ thay đổi thành công!" });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var paragraph = await _context.TblParagraphs.FindAsync(id);
            if (paragraph == null)
                return Json(new { success = false, message = "Paragraph not found" });

            int chapterId = paragraph.ChapterId;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.TblParagraphs.Remove(paragraph);
                await _context.SaveChangesAsync();

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC sp_ReindexParagraphOrder @ChapID = {chapterId}");

                await transaction.CommitAsync();
                return Json(new { success = true, message = "Deleted and reindexed successfully" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Delete failed: {innerError}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No paragraphs selected" });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var paragraphs = await _context.TblParagraphs.Where(x => ids.Contains(x.ParagraphId)).ToListAsync();
                if (!paragraphs.Any())
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "No valid paragraphs found" });
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
                return Json(new { success = true, message = $"Đã xóa thành công {paragraphs.Count} đoạn!", deleted = paragraphs.Select(x => x.ParagraphId) });
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
        public async Task<IActionResult> UploadIllustration(IFormFile file, [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn một file ảnh hợp lệ!" });
            }

            // 1. Kiểm tra đuôi file
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return Json(new { success = false, message = "Chỉ chấp nhận file ảnh định dạng jpg, jpeg, png, webp, gif!" });
            }

            try
            {
                // 2. Định vị thư mục đích wwwroot\assets\image\stories
                var uploadFolder = Path.Combine(env.WebRootPath, "assets", "image", "stories");
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                // 3. Đặt tên file ngẫu nhiên để không bị trùng lặp
                var fileName = $"{Guid.NewGuid():N}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                var fullPath = Path.Combine(uploadFolder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 4. Trả về đường dẫn web chuẩn tương đối
                var relativeUrl = $"/assets/image/stories/{fileName}";

                return Json(new
                {
                    success = true,
                    url = relativeUrl,
                    message = "Tải ảnh lên thành công!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi upload: " + ex.Message });
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
        public async Task<IActionResult> EnqueueImportAi(int chapterId, IFormFile fileEnglish, string targetLanguages = "Vietnamese")
        {
            if (fileEnglish == null || fileEnglish.Length == 0 || chapterId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn Chapter và file Tiếng Anh hợp lệ!" });
            }

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "temp_uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var tempFilePath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{fileEnglish.FileName}");
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await fileEnglish.CopyToAsync(stream);
            }

            // Đẩy Job vào Hangfire xử lý danh sách ngôn ngữ được chọn
            var jobId = BackgroundJob.Enqueue<IParagraphAiProcessingService>(service =>
                service.ProcessChapterFileJob(chapterId, tempFilePath, targetLanguages));

            return Json(new
            {
                success = true,
                jobId,
                message = "Đã gửi vào hàng đợi xử lý ngầm Hangfire! Hệ thống đang tự động đánh BlockType và dịch thuật."
            });
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

            // Sửa thành cú pháp Regex.Replace chuẩn của C#
            string clean = Regex.Replace(html, @"<\/?p[^>]*>", "");
            return Regex.Replace(clean, @"<br\s*\/?>", " ").Trim();
        }
        public class MergeParagraphRequest
{
    public int TargetParagraphId { get; set; }
    // "prev" = gộp với đoạn trước, "next" = gộp với đoạn sau
    public string Direction { get; set; } = "prev"; 
    public bool RemoveImages { get; set; } = true;
}

[HttpPost]
public async Task<IActionResult> MergeParagraphs([FromBody] MergeParagraphRequest request)
{
    if (request == null || request.TargetParagraphId <= 0)
        return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

    var target = await _context.TblParagraphs.FirstOrDefaultAsync(p => p.ParagraphId == request.TargetParagraphId);
    if (target == null)
        return Json(new { success = false, message = "Không tìm thấy đoạn văn chỉ định." });

    int chapterId = target.ChapterId;

    // Tìm đoạn liền trước hoặc liền sau dựa vào ParagraphOrder
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
        string dirText = request.Direction.ToLower() == "prev" ? "phía trước" : "phía sau";
        return Json(new { success = false, message = $"Không có đoạn văn {dirText} để gộp!" });
    }

    // Xác định thứ tự ghép: đoạn có Order nhỏ hơn sẽ đứng trước
    TblParagraph first = target.ParagraphOrder < other.ParagraphOrder ? target : other;
    TblParagraph second = target.ParagraphOrder < other.ParagraphOrder ? other : target;

    // Hàm phụ trợ nối chuỗi và dọn dẹp thẻ ảnh nếu được yêu cầu
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
        // Gộp nội dung từ đoạn thứ 2 dồn vào đoạn thứ nhất
        first.English = CombineText(first.English, second.English);
        first.Vietnamese = CombineText(first.Vietnamese, second.Vietnamese);
        first.Chinese = CombineText(first.Chinese, second.Chinese);
        first.Japanese = CombineText(first.Japanese, second.Japanese);
        first.French = CombineText(first.French, second.French);

        // Xóa đoạn thứ 2
        _context.TblParagraphs.Remove(second);
        await _context.SaveChangesAsync();

        // GỌI THỦ TỤC sp_ReindexParagraphOrder ĐỂ ĐÁNH LẠI STT KHÔNG BỊ KHUYẾT
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC [dbo].[sp_ReindexParagraphOrder] @ChapID = {chapterId}");

        await transaction.CommitAsync();

        return Json(new { success = true, message = "Đã gộp đoạn văn và chuẩn hóa lại số thứ tự thành công!" });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        return Json(new { success = false, message = $"Lỗi khi gộp đoạn: {msg}" });
    }
}
    }

}
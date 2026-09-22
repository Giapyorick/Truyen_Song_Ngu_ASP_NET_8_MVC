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
                return Json(new { success = false, message = "Không tìm thấy chương hoặc truyện." });
            }

            var rawLangs = (chapter.Story.Lang ?? "vi")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(l => l.Trim().ToLower())
                .Distinct()
                .ToList();

            var langList = rawLangs.Select(code => new
            {
                code = NormalizeLangKey(code),
                rawCode = code,
                name = GetLangDisplayName(code)
            }).ToList();

            return Json(new { success = true, languages = langList });
        }

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
                    .OrderBy(x => x.StoryId)
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

        // Action duy nhất tiếp nhận file và xếp hàng vào Hangfire
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
        private static string NormalizeLangKey(string code) => code switch
        {
            "vi" or "vie" or "vietnamese" => "vietnamese",
            "en" or "eng" or "english" or "us" => "english",
            "zh" or "zho" or "cn" or "chinese" => "chinese",
            "ja" or "jp" or "jpn" or "japanese" => "japanese",
            "fr" or "fra" or "french" => "french",
            _ => code
        };

        private static string GetLangDisplayName(string code) => code switch
        {
            "vietnamese" or "vi" => "Tiếng Việt 🇻🇳",
            "english" or "en" => "English 🇺🇸",
            "chinese" or "zh" or "cn" => "Tiếng Trung 🇨🇳",
            "japanese" or "ja" or "jp" => "Tiếng Nhật 🇯🇵",
            "french" or "fr" => "Tiếng Pháp 🇫🇷",
            _ => code.ToUpper()
        };

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
    }
}
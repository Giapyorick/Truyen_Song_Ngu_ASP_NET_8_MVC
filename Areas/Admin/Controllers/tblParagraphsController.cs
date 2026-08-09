using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class tblParagraphsController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly TruyenSongNguContext _context;
        private readonly IAiTranslationService _aiService;

        public tblParagraphsController(TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment, IAiTranslationService aiService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _aiService = aiService;
        }
        [HttpGet]
        public IActionResult Paragraphs()
        {
            return View(); 
        }
        [HttpGet]
        public async Task<IActionResult> GetStoriesForSelect(){
            var list = await _context.TblStories
                .AsNoTracking()
                .Select(x => new{
                    id = x.StoryId,
                    name = x.Title
                })
                .ToListAsync();
            return Json(list);
        }
        [HttpGet]
        public async Task<IActionResult> GetChaptersForSelect(int id){
            var list = await _context.TblChapters
                .AsNoTracking()
                .Where(x => x.StoryId == id)
                .Select(x => new{
                    id = x.ChapterId,
                    name = x.Title
                })
                .ToListAsync();
            return Json(list);
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
        public async Task<IActionResult> List(string? search, int? storyId, int? cstoryId, int? chapterId, int page = 1, int pageSize = 10)
        {
            try
            {
                // Lấy storyId từ 1 trong 2 tham số gửi lên
                int? filterStoryId = storyId ?? cstoryId;

                var query = _context.TblParagraphs
                    .AsNoTracking()
                    .Select(p => new
                    {
                        p.ParagraphId,
                        p.ParagraphOrder,
                        // Dùng ?? "" để đảm bảo không bị null khi serialization
                        English = p.English ?? "",
                        Vietnamese = p.Vietnamese ?? "",
                        Chinese = p.Chinese ?? "",
                        Japanese = p.Japanese ?? "",
                        French = p.French ?? "",

                        ChapterId = p.ChapterId,
                        // Bọc kiểm tra null an toàn cho navigation property
                        ChapterTitle = p.Chap != null ? p.Chap.Title : "N/A",

                        StoryId = (p.Chap != null && p.Chap.Story != null) ? (int?)p.Chap.StoryId : null,
                        StoryTitle = (p.Chap != null && p.Chap.Story != null) ? p.Chap.Story.Title : "N/A"
                    })
                    .AsQueryable();

                // 1. Lọc theo chuỗi tìm kiếm
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(x =>
                        (x.English != null && x.English.Contains(search)) ||
                        (x.Vietnamese != null && x.Vietnamese.Contains(search)) ||
                        (x.Chinese != null && x.Chinese.Contains(search)) ||
                        (x.Japanese != null && x.Japanese.Contains(search)) ||
                        (x.French != null && x.French.Contains(search))
                    );
                }

                // 2. Lọc theo StoryId (Bổ sung thêm)
                if (filterStoryId.HasValue && filterStoryId.Value > 0)
                {
                    query = query.Where(x => x.StoryId == filterStoryId.Value);
                }

                // 3. Lọc theo ChapterId
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
                // Trả về lỗi chi tiết để debug thay vì chỉ trả về 500 chung chung
                return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> ImportExcel(
            int chapId,
            IFormFile? fileEnglish,
            IFormFile? fileVietnamese,
            IFormFile? fileChinese,
            IFormFile? fileJapanese,
            IFormFile? fileFrench)
        {
            var files = new Dictionary<string, IFormFile?>
            {
                { "English", fileEnglish },
                { "Vietnamese", fileVietnamese },
                { "Chinese", fileChinese },
                { "Japanese", fileJapanese },
                { "French", fileFrench }
            };

            var activeFiles = files.Where(f => f.Value != null && f.Value.Length > 0).ToDictionary(f => f.Key, f => f.Value!);

            if (activeFiles.Count == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 file dữ liệu để import!" });
            }

            var parsedData = new Dictionary<string, List<string>>();
            foreach (var item in activeFiles)
            {
                parsedData[item.Key] = ProcessFileImport(item.Value);
            }

            int expectedCount = parsedData.First().Value.Count;
            var mismatched = parsedData.Where(p => p.Value.Count != expectedCount).ToList();

            if (mismatched.Any())
            {
                string details = string.Join(", ", parsedData.Select(p => $"{p.Key}: {p.Value.Count} câu"));
                return Json(new
                {
                    success = false,
                    message = $"Số lượng câu giữa các file không đồng bộ! Chi tiết: ({details})"
                });
            }

            int currentMaxOrder = await _context.TblParagraphs
                .Where(p => p.ChapterId == chapId)
                .Select(p => (int?)p.ParagraphOrder)
                .MaxAsync() ?? 0;

            for (int i = 0; i < expectedCount; i++)
            {
                var paragraph = new TblParagraph
                {
                    ChapterId = chapId,
                    ParagraphOrder = currentMaxOrder + i + 1,
                    English = parsedData.ContainsKey("English") ? parsedData["English"][i].Trim() : null,
                    Vietnamese = parsedData.ContainsKey("Vietnamese") ? parsedData["Vietnamese"][i].Trim() : null,
                    Chinese = parsedData.ContainsKey("Chinese") ? parsedData["Chinese"][i].Trim() : null,
                    Japanese = parsedData.ContainsKey("Japanese") ? parsedData["Japanese"][i].Trim() : null,
                    French = parsedData.ContainsKey("French") ? parsedData["French"][i].Trim() : null
                };

                _context.TblParagraphs.Add(paragraph);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Đã nhập thành công {expectedCount} đoạn văn!" });
        }

        private List<string> ProcessFileImport(IFormFile file)
        {
            string ext = Path.GetExtension(file.FileName).ToLower();

            if (ext == ".txt")
            {
                return ReadSentencesFromTxt(file);
            }

            return ReadSentencesFromExcel(file);
        }

        private List<string> ReadSentencesFromExcel(IFormFile file)
        {
            var sentences = new List<string>();
            using (var stream = file.OpenReadStream())
            {
                using (var package = new ExcelPackage(stream))
                {
                    var sheet = package.Workbook.Worksheets[0];
                    var rowCount = sheet.Dimension?.Rows ?? 0;

                    for (int row = 1; row <= rowCount; row++)
                    {
                        string cellValue = sheet.Cells[row, 1].Text;
                        if (string.IsNullOrEmpty(cellValue)) continue;

                        var splitContent = Regex.Split(cellValue, @"(?<=[.!?])\s*")
                                                .Where(s => !string.IsNullOrWhiteSpace(s));

                        sentences.AddRange(splitContent);
                    }
                }
            }
            return sentences;
        }

        private List<string> ReadSentencesFromTxt(IFormFile file)
        {
            var sentences = new List<string>();

            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                string fileContent = reader.ReadToEnd();

                if (!string.IsNullOrEmpty(fileContent))
                {
                    var splitContent = Regex.Split(fileContent, @"(?<=[.!?。！？])\s*")
                        .Where(s => !string.IsNullOrWhiteSpace(s));

                    sentences.AddRange(splitContent);
                }
            }

            return sentences;
        }
        [HttpPost]
        public async Task<IActionResult> Update(ParagraphsViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid data" });
            var chapterExists = await _context.TblChapters
                .AnyAsync(x => x.ChapterId == model.ChapterId);

            if (!chapterExists)
            {
                return Json(new { success = false, message = "Chapter không tồn tại" });
            }

            var paragraph = await _context.TblParagraphs
                .FirstOrDefaultAsync(x => x.ParagraphId == model.ParagraphId);

            if (paragraph == null)
                return Json(new { success = false, message = "Paragraph not found" });

            paragraph.ChapterId = model.ChapterId;
            paragraph.ParagraphOrder = model.ParagraphOrder;
            paragraph.English = model.English;
            paragraph.Vietnamese = model.Vietnamese;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Updated successfully" });
        }


        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var paragraph = await _context.TblParagraphs.FindAsync(id);
            if (paragraph == null)
                return Json(new { success = false, message = "Paragraph not found" });

            _context.TblParagraphs.Remove(paragraph);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Deleted successfully" });
        }
       [HttpPost]
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No paragraphs selected" });

            var paragraphs = await _context.TblParagraphs
                .Where(x => ids.Contains(x.ParagraphId))
                .ToListAsync();

            if (!paragraphs.Any())
                return Json(new { success = false, message = "No valid paragraphs found" });

            _context.TblParagraphs.RemoveRange(paragraphs);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                deleted = paragraphs.Select(x => x.ParagraphId)
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string? search, int? chapterId)
        {
            var query = _context.TblParagraphs
                .AsNoTracking()
                .AsQueryable();
            if (chapterId.HasValue && chapterId.Value > 0)
            {
                query = query.Where(p => p.ChapterId == chapterId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.English.Contains(search) ||
                    p.Vietnamese.Contains(search)
                );
            }
            var data = await query
                .OrderBy(p => p.ParagraphOrder)
                .Select(p => new
                {
                    p.ChapterId,
                    ChapterTitle = p.Chap.Title,
                    StoryTitle = p.Chap.Story.Title,
                    p.ParagraphId,
                    p.ParagraphOrder,
                    p.English,
                    p.Vietnamese
                })
                .ToListAsync();

            

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Paragraphs");

            // Title
            ws.Cells[1, 1].Value = "LIST OF PARAGRAPHS";
            ws.Cells[1, 1, 1, 6].Merge = true;
            ws.Cells[1, 1].Style.Font.Size = 18;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1].Value = $"Export date: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            ws.Cells[2, 1, 2, 6].Merge = true;

            ws.Cells[3, 1].Value = $"Total paragraphs: {data.Count}";
            ws.Cells[3, 1, 3, 6].Merge = true;
            ws.Cells[3, 1].Style.Font.Bold = true;

            string[] headers = { "Id", "Story", "Chapter", "Order", "English", "Vietnamese" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cells[5, i + 1].Value = headers[i];

            int row = 6;
            foreach (var p in data)
            {
                ws.Cells[row, 1].Value = p.ParagraphId;
                ws.Cells[row, 2].Value = p.StoryTitle;
                ws.Cells[row, 3].Value = p.ChapterTitle;
                ws.Cells[row, 4].Value = p.ParagraphOrder;
                ws.Cells[row, 5].Value = p.English;
                ws.Cells[row, 6].Value = p.Vietnamese;
                row++;
            }

            ws.Cells.AutoFitColumns();

            return File(
                await package.GetAsByteArrayAsync(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Paragraphs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }
        [HttpPost]
        public async Task<IActionResult> TranslateEnglishFileWithAI(IFormFile fileEnglish, string targetLanguage)
        {
            try
            {
                if (fileEnglish == null || fileEnglish.Length == 0)
                    return Json(new { success = false, message = "Vui lòng chọn file Tiếng Anh!" });

                List<string> englishLines = ProcessFileImport(fileEnglish);
                if (!englishLines.Any())
                    return Json(new { success = false, message = "File Tiếng Anh rỗng!" });

                // Chuyển mảng câu thành JSON
                string jsonInput = System.Text.Json.JsonSerializer.Serialize(englishLines);

                // PROMPT SIÊU NGHIÊM NGẶT - ÉP AI KHÔNG ĐƯỢC GỘP CÂU
                string prompt = $@"You are an exact line-by-line translator.
Translate the following JSON array of sentences from English into {targetLanguage}.

STRICT RULES:
1. Output MUST be a valid JSON array of strings.
2. The output JSON array MUST contain EXACTLY {englishLines.Count} elements, matching the input count 1-to-1.
3. NEVER merge sentences. Never combine multiple input lines into one line.
4. Return ONLY the raw JSON array. Do NOT wrap in markdown like ```json ... ```.

Input JSON:
{jsonInput}";

                string resultText = await _aiService.GetAiResponse(prompt);

                // Làm sạch mã markdown nếu AI vô tình trả về
                resultText = resultText.Replace("```json", "").Replace("```", "").Trim();

                var translatedLines = System.Text.Json.JsonSerializer.Deserialize<List<string>>(resultText);

                // Kiểm tra xem AI có dịch đủ số dòng hay không
                if (translatedLines == null || translatedLines.Count != englishLines.Count)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"AI dịch lệch số câu! (Đầu vào: {englishLines.Count} câu, AI trả về: {translatedLines?.Count ?? 0} câu). Vui lòng thử lại!"
                    });
                }

                return Json(new
                {
                    success = true,
                    translatedData = translatedLines,
                    message = $"Đã dịch thành công chuẩn {translatedLines.Count} câu sang {targetLanguage}!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi AI: {ex.Message}" });
            }
        }
    }
}
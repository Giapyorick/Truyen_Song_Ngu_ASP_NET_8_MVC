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

        public tblParagraphsController(TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
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
        public async Task<IActionResult> List( string? search, int? chapterId, int page = 1, int pageSize = 10)
        {
            try
            {
                var query = _context.TblParagraphs
                    .AsNoTracking()
                    .Select(p => new
                    {
                        p.ParagraphId,
                        p.ParagraphOrder,
                        p.English,
                        p.Vietnamese,

                        ChapterId = p.ChapterId,
                        ChapterTitle = p.Chap.Title,

                        StoryId = p.Chap.StoryId,
                        StoryTitle = p.Chap.Story.Title
                    })
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(x =>
                        x.English.Contains(search) ||
                        x.Vietnamese.Contains(search)
                    );
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
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ImportExcel(int chapId, IFormFile fileEnglish, IFormFile fileVietnamese)
        {
            if (fileEnglish == null || fileVietnamese == null)
            {
                return Json(new { success = false, message = "Vui lòng chọn đầy đủ cả 2 file Anh và Việt!" });
            }

            // Tự động nhận diện và đọc dữ liệu dựa trên đuôi file (.xlsx/.xls hoặc .txt)
            var listEnglish = ProcessFileImport(fileEnglish);
            var listVietnamese = ProcessFileImport(fileVietnamese);

            if (listEnglish.Count != listVietnamese.Count)
            {
                return Json(new
                {
                    success = false,
                    message = $"Số lượng câu không khớp! File Anh: {listEnglish.Count} câu, File Việt: {listVietnamese.Count} câu."
                });
            }

            int currentMaxOrder = await _context.TblParagraphs
                .Where(p => p.ChapterId == chapId)
                .Select(p => (int?)p.ParagraphOrder)
                .MaxAsync() ?? 0;

            for (int i = 0; i < listEnglish.Count; i++)
            {
                var paragraph = new TblParagraph
                {
                    ChapterId = chapId,
                    ParagraphOrder = currentMaxOrder + i + 1,
                    English = listEnglish[i].Trim(),
                    Vietnamese = listVietnamese[i].Trim()
                };
                _context.TblParagraphs.Add(paragraph);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
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
                    var splitContent = Regex.Split(fileContent, @"(?<=[.!?])\s*")
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


	}
}
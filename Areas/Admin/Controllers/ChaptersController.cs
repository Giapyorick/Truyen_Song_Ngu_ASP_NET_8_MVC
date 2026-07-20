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
        public async Task<IActionResult> GetStoriesForSelect()
        {
           return Json(_context.TblStories
            .Select(x => new {
                id = x.StoryId,
                name = x.Title
            }).ToList());
        }
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var chapter = await _context.TblChapters
				.Include(x => x.Story)
                .Select(u => new {
                        u.ChapterId,
                        u.Title,
                        u.StoryId,
						StoryTitle = u.Story.Title,
                        u.ChapterNumber,
                        CreateDate = u.CreatedDate.HasValue ? u.CreatedDate.Value.ToString("yyyy-MM-dd") : ""
                    })
                .FirstOrDefaultAsync(x => x.ChapterId == id);
                

            if (chapter == null) return NotFound();
            return Json(chapter);
        }

        [HttpGet]
        public async Task<IActionResult> List(string? search, string? storyId, int page = 1, int pageSize = 5)
        {
            try
    		{
				var query = _context.TblChapters
					.Select(c => new
					{
						c.ChapterId,
						c.Title,
						c.StoryId,
						StoryTitle = c.Story.Title,
						c.ChapterNumber,
						CreateDate = c.CreatedDate.HasValue ? c.CreatedDate.Value.ToString("yyyy-MM-dd") : ""
					});

				if (!string.IsNullOrEmpty(search))
					query = query.Where(x => x.Title.Contains(search) || x.StoryTitle.Contains(search));

				if (!string.IsNullOrEmpty(storyId) && storyId != "all")
				{
					int stId = int.Parse(storyId);
					query = query.Where(x => x.StoryId == stId);
				}

				int totalItems = await query.CountAsync();
				int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

				var data = await query
					.OrderBy(x => x.StoryId)
					.ThenBy(x => x.ChapterNumber)
					.Skip((page - 1) * pageSize)
					.Take(pageSize)
					.ToListAsync();

				return Json(new
				{
					chapters = data,
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


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}
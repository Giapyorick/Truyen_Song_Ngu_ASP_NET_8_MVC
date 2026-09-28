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
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class tblCategoriesController : Controller
    {
        private readonly ILogger<tblUsersController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly TruyenSongNguContext _context;

        public tblCategoriesController(ILogger<tblUsersController> logger, TruyenSongNguContext context, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }
        [HttpGet]
        public IActionResult Categories()
        {
            return View(); 
        }
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _context.TblCategories
                .Select(u => new {
                        u.CategoryId,
                        u.Name,
                        u.Description,
                        u.Status,
                    })
                .FirstOrDefaultAsync(x => x.CategoryId == id);
                

            if (category == null) return NotFound();
            return Json(category);
        }

        [HttpGet]
        public async Task<IActionResult> List(string? search, string? status, int page = 1, int pageSize = 5)
        {
            try 
            {
                var query = _context.TblCategories.AsQueryable().AsNoTracking();

                if (!string.IsNullOrEmpty(search)) {
                    query = query.Where(u => u.Name.Contains(search));
                }
                
                if (!string.IsNullOrEmpty(status) && status != "all") {
                    query = query.Where(u => u.Status == status);
                }

                int totalItems = await query.CountAsync(); 
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                // Project only required fields to avoid serializing navigation properties and extra lazy-loading queries
                var data = await query
                    .OrderByDescending(u => u.CategoryId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new {
                        u.CategoryId,
                        u.Name,
                        u.Description,
                        u.Status
                    })
                    .ToListAsync();

                return Json(new {
                    categories = data,
                    currentPage = page,
                    totalPages = totalPages,
                    totalItems = totalItems
                });
            }
            catch (Exception ex) 
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPost]
		public async Task<IActionResult> Add(CategoriesViewModel model)
		{
			if (!ModelState.IsValid)
				return Json(new { success = false, message = "Data errors" });

			try
			{
				var item = new TblCategory
				{
					Name = model.Name,
					Description = model.Description,
					Status = model.Status
				};

				await _context.TblCategories.AddAsync(item);
				await _context.SaveChangesAsync();

				return Json(new { success = true, message = "New category added successfully!" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = "An errors occurred: " + ex.Message });
			}
		}
		[HttpPost]
		public async Task<IActionResult> Update(CategoriesViewModel model)
		{
			var category = await _context.TblCategories.FindAsync(model.CategoryId);
			if (category == null)
				return Json(new { success = false, message = "Not found any category" });

			category.Name = model.Name;
			category.Description = model.Description;
			category.Status = model.Status;

			await _context.SaveChangesAsync();

			return Json(new { success = true, message = "Category updated successfully!" });
		}


		[HttpPost]
		public async Task<IActionResult> Delete(int id)
		{
			try
			{
				var category = await _context.TblCategories.FindAsync(id);
				if (category == null)
					return Json(new { success = false, message = "Not found any category." });

				_context.TblCategories.Remove(category);
				await _context.SaveChangesAsync();

				return Json(new { success = true, message = "Removed category successfully!" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = "System error: " + ex.Message });
			}
		}

		[HttpPost]
		public IActionResult DeleteMultiple(List<int> ids)
		{
			if (ids == null || !ids.Any())
				return BadRequest("No categories selected.");

			var cannotDelete = new List<int>();
			var canDelete = new List<TblCategory>();

			foreach (var id in ids)
			{
				bool isReferenced = _context.TblCategoryOfStories.Any(x => x.CategoryId == id);

				if (isReferenced)
				{
					cannotDelete.Add(id);
				}
				else
				{
					var category = _context.TblCategories.FirstOrDefault(x => x.CategoryId == id);
					if (category != null)
						canDelete.Add(category);
				}
			}

			if (canDelete.Any())
			{
				_context.TblCategories.RemoveRange(canDelete);
				_context.SaveChanges();
			}

			return Ok(new
			{
				deleted = canDelete.Select(x => x.CategoryId),
				blocked = cannotDelete
			});
		}

		[HttpGet]
		public async Task<IActionResult> ExportToExcel(string search, string status)
		{
			var query = _context.TblCategories.AsQueryable();

			if (!string.IsNullOrEmpty(search))
				query = query.Where(c => c.Name.Contains(search));

			if (!string.IsNullOrEmpty(status) && status != "all")
				query = query.Where(c => c.Status == status);

			var data = await query.ToListAsync();

			ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

			using var package = new ExcelPackage();
			var ws = package.Workbook.Worksheets.Add("Categories");
			int totalCols = 6;
			ws.Cells[1, 1].Value = "LIST OF CATEGORIES";
			ws.Cells[1, 1, 1, 4].Merge = true;
			ws.Cells[1, 1].Style.Font.Size = 18;
			ws.Cells[1, 1].Style.Font.Bold = true;
			ws.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

			ws.Cells[2, 1].Value = $"Export date: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
			ws.Cells[2, 1, 2, totalCols].Merge = true;
			ws.Cells[2, 1].Style.Font.Italic = true;

			ws.Cells[3, 1].Value = $"Total categories: {data.Count}";
			ws.Cells[3, 1, 3, totalCols].Merge = true;
			ws.Cells[3, 1].Style.Font.Bold = true;

			string[] headers = { "Id", "Name", "Description", "Status" };
			for (int i = 0; i < headers.Length; i++)
				ws.Cells[5, i + 1].Value = headers[i];

			int row = 6;
			foreach (var c in data)
			{
				ws.Cells[row, 1].Value = c.CategoryId;
				ws.Cells[row, 2].Value = c.Name;
				ws.Cells[row, 3].Value = c.Description;
				ws.Cells[row, 4].Value = c.Status;
				row++;
			}

			ws.Cells.AutoFitColumns();

			return File(
				await package.GetAsByteArrayAsync(),
				"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
				$"Categories_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
			);
		}

		[HttpPost]
		public async Task<IActionResult> ImportExcel(IFormFile file)
		{
			if (file == null || file.Length <= 0)
				return Json(new { success = false, message = "Please select file!" });

			ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

			using var stream = new MemoryStream();
			await file.CopyToAsync(stream);

			using var package = new ExcelPackage(stream);
			var worksheet = package.Workbook.Worksheets[0];
			int rowCount = worksheet.Dimension.Rows;

			var list = new List<TblCategory>();

			for (int row = 6; row <= rowCount; row++)
			{
				var item = new TblCategory
				{
					Name = worksheet.Cells[row, 1].Value?.ToString(),
					Description = worksheet.Cells[row, 2].Value?.ToString(),
					Status = worksheet.Cells[row, 3].Value?.ToString() ?? "Active"
				};

				if (!string.IsNullOrEmpty(item.Name))
					list.Add(item);
			}

			if (list.Any())
			{
				_context.TblCategories.AddRange(list);
				await _context.SaveChangesAsync();
				return Json(new { success = true, message = $"Imported {list.Count} categories successfully!" });
			}

			return Json(new { success = false, message = "No valid data was found in the file." });
		}



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}
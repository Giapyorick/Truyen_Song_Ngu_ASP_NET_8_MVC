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
    [AdminAuthorize]
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
                .Include(c => c.TblCategoryTranslations)
                .Where(x => x.CategoryId == id)
                .FirstOrDefaultAsync();

            if (category == null) return NotFound();

            var transVi = category.TblCategoryTranslations.FirstOrDefault(t => t.LanguageCode == "vi-VN");

            return Json(new
            {
                category.CategoryId,
                NameEn = category.Name ?? "",              // Tiếng Anh lấy thẳng bảng gốc
                DescEn = category.Description ?? "",       // Tiếng Anh lấy thẳng bảng gốc
                NameVi = transVi?.Name ?? "",              // Tiếng Việt lấy từ bảng translation
                DescVi = transVi?.Description ?? "",
                category.Status
            });
        }
        [HttpGet]
        public async Task<IActionResult> List(string? search, string? status, string? culture, int page = 1, int pageSize = 5)
        {
            try
            {
                // 1. Chuẩn hóa Culture
                var rawCulture = !string.IsNullOrWhiteSpace(culture)
                    ? culture.Trim()
                    : System.Globalization.CultureInfo.CurrentUICulture.Name;

                bool isVietnamese = rawCulture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
                string langPrefix = isVietnamese ? "vi" : "en";
                string targetCulture = isVietnamese ? "vi-VN" : "en-US";

                System.Diagnostics.Debug.WriteLine($"[CATEGORIES LIST DEBUG] rawCulture={rawCulture} | langPrefix={langPrefix} | targetCulture={targetCulture}");

                // 2. Query chuẩn bảng Thể loại (TblCategories)
                var query = _context.TblCategories.AsNoTracking().AsQueryable();

                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    query = query.Where(u => u.Status == status);
                }

                // 3. Lấy bản dịch theo đúng ngôn ngữ được yêu cầu
                var projectedQuery = query.Select(c => new
                {
                    c.CategoryId,
                    Name = c.TblCategoryTranslations
                            .Where(t => t.LanguageCode.StartsWith(langPrefix))
                            .Select(t => t.Name)
                            .FirstOrDefault() ?? c.Name,

                    Description = c.TblCategoryTranslations
                                   .Where(t => t.LanguageCode.StartsWith(langPrefix))
                                   .Select(t => t.Description)
                                   .FirstOrDefault() ?? c.Description,

                    c.Status
                });

                if (!string.IsNullOrEmpty(search))
                {
                    projectedQuery = projectedQuery.Where(u =>
                        u.Name.Contains(search) ||
                        (u.Description != null && u.Description.Contains(search)));
                }

                int totalItems = await projectedQuery.CountAsync();
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                var data = await projectedQuery
                    .OrderByDescending(u => u.CategoryId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"[CATEGORIES LIST DEBUG] Total items={totalItems}, Count in page={data.Count}");
                if (data.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"[CATEGORIES LIST DEBUG] First item Name: {data.First().Name}");
                }

                return Json(new
                {
                    categories = data,
                    currentPage = page,
                    totalPages = totalPages,
                    totalItems = totalItems,
                    debugCulture = targetCulture
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CATEGORIES LIST ERROR] {ex.Message}");
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPost]
        [AdminRoleAuthorize]
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
		[AdminRoleAuthorize]
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
        [AdminRoleAuthorize]
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
        [AdminRoleAuthorize]
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
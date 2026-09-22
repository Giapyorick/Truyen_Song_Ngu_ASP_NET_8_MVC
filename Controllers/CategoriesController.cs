using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly TruyenSongNguContext _context;

        public CategoriesController(TruyenSongNguContext context)
        {
            _context = context;
        }

        // GET: /Categories hoặc /Categories?categoryId=1
        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId)
        {
            var categories = await _context.TblCategories
                .AsNoTracking()
                .Where(c => !string.IsNullOrEmpty(c.Name))
                .Select(c => new CategoryItemDto
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description,
                    StoryCount = _context.TblCategoryOfStories.Count(cs => cs.CategoryId == c.CategoryId)
                })
                .ToListAsync();

            if (!categoryId.HasValue && categories.Any())
            {
                categoryId = categories.First().CategoryId;
            }

            ViewBag.Categories = categories;
            ViewBag.SelectedCategoryId = categoryId;

            var selectedCat = categories.FirstOrDefault(c => c.CategoryId == categoryId);
            ViewBag.SelectedCategoryName = selectedCat?.Name ?? "";
            ViewBag.SelectedCategoryDesc = selectedCat?.Description ?? "";

            var stories = categoryId.HasValue
                ? await GetStoriesByCategoryId(categoryId.Value)
                : new List<StoryListViewModel>();

            return View(stories);
        }

        // AJAX lấy Partial View danh sách truyện
        [HttpGet]
        public async Task<IActionResult> GetStoriesPartial(int categoryId)
        {
            var stories = await GetStoriesByCategoryId(categoryId);
            return PartialView("_StoryListPartial", stories);
        }

        private async Task<List<StoryListViewModel>> GetStoriesByCategoryId(int categoryId)
        {
            return await (from cs in _context.TblCategoryOfStories
                          join s in _context.TblStories on cs.StoryId equals s.StoryId
                          where cs.CategoryId == categoryId
                          select new StoryListViewModel
                          {
                              StoryID = s.StoryId,
                              Title = s.Title,
                              Img = s.Img,
                              Lang = s.Lang,
                              Rate = s.Rate,
                              Likes = s.Likes,
                              CountFolower = s.CountFolower,
                              HasProgress = false,
                              LatestChapters = _context.TblChapters
                                  .Where(ch => ch.StoryId == s.StoryId)
                                  .OrderByDescending(ch => ch.ChapterNumber)
                                  .Take(3)
                                  .Select(ch => new LatestChapterItemViewModel
                                  {
                                      ChapterId = ch.ChapterId,
                                      ChapterNumber = ch.ChapterNumber,
                                      ChapterTitle = ch.Title
                                  }).ToList()
                          })
                          .AsNoTracking()
                          .ToListAsync();
        }
    }
}
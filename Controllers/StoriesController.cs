using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;

namespace WebTruyenTranh.Controllers
{
    public class StoriesController : Controller
    {
        private readonly ILogger<StoriesController> _logger;
        private readonly TruyenSongNguContext _context;

        public StoriesController(ILogger<StoriesController> logger, TruyenSongNguContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index() //Components/StoriesComponent.cs
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var stories = _context.TblStories
            .Select(s => new StoryListViewModel
            {
                StoryID = s.StoryId,
                Title = s.Title ?? "Chưa xác định",
                Img = s.Img,
                HasProgress = _context.TblUserReadingProgresses
                    .Any(p => p.UserId == userId && p.StoryId == s.StoryId),

                LastChapterId = _context.TblUserReadingProgresses
                    .Where(p => p.UserId == userId && p.StoryId == s.StoryId)
                    .Select(p => (int?)p.LastChapterId)
                    .FirstOrDefault(),

                Categories = s.TblCategoryOfStories
                    .Select(c => c.Category.Name ?? "Chưa xác định") 
                    .ToList()
            })
            .ToList();

        return View(stories);
        }
        public IActionResult Detail(int id)
        {
            var story = _context.TblStories
                .Include(s => s.TblChapters)
                .Include(s => s.Author)
                .Include(s => s.TblCategoryOfStories)
                .ThenInclude(a => a.Category)
                .FirstOrDefault(s => s.StoryId == id);

            return View(story);
        }
        public IActionResult Read(int id)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var chapter = _context.TblChapters.FirstOrDefault(c => c.ChapterId == id);

            var progress = _context.TblUserReadingProgresses
                .FirstOrDefault(p => p.UserId == userId && p.StoryId == chapter.StoryId);

            if (progress == null)
            {
                _context.TblUserReadingProgresses.Add(new TblUserReadingProgress
                {
                    UserId = userId,
                    StoryId = chapter.StoryId,
                    LastChapterId = id,
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                progress.LastChapterId = id;
                progress.UpdatedAt = DateTime.Now;
            }

            _context.SaveChanges();

            return View(chapter);
        }



    }
    
}
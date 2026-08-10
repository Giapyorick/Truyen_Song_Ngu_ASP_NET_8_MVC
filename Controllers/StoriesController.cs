using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient; 
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

        public async Task<IActionResult> Index() 
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var stories = await _context.TblStories
                .Select(s => new StoryListViewModel
                {
                    StoryID = s.StoryId,
                    Title = s.Title ?? "Chưa xác định",
                    Img = s.Img,
                    HasProgress = userId > 0 && _context.TblUserReadingProgresses
                        .Any(p => p.UserId == userId && p.StoryId == s.StoryId),

                    LastChapterId = userId > 0 ? _context.TblUserReadingProgresses
                        .Where(p => p.UserId == userId && p.StoryId == s.StoryId)
                        .Select(p => (int?)p.LastChapterId)
                        .FirstOrDefault() : null,

                    Categories = s.TblCategoryOfStories
                        .Select(c => c.Category.Name ?? "Chưa xác định")
                        .ToList()
                })
                .ToListAsync();

            return View(stories);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var story = await _context.TblStories
                .Include(s => s.TblChapters)
                .Include(s => s.Author)
                .Include(s => s.TblCategoryOfStories)
                    .ThenInclude(a => a.Category)
                .FirstOrDefaultAsync(s => s.StoryId == id);

            if (story == null)
            {
                return NotFound();
            }

            return View(story);
        }

        public async Task<IActionResult> Read(int id)
        {
            // 1. Lấy UserId từ Session
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            // 2. IN LOG TRỰC TIẾP RA CONSOLE / OUTPUT (Không lo bị chặn)
            System.Diagnostics.Debug.WriteLine($"===================> CHECK SESSION: UserId = {userId}, ChapterId = {id}");
            Console.WriteLine($"===================> CHECK SESSION: UserId = {userId}, ChapterId = {id}");

            var chapter = await _context.TblChapters.FirstOrDefaultAsync(c => c.ChapterId == id);
            if (chapter == null)
            {
                Console.WriteLine($"===================> KHÔNG TÌM THẤY CHAPTER: {id}");
                return NotFound();
            }

            if (userId > 0)
            {
                try
                {
                    Console.WriteLine("===================> CHUẨN BỊ GỌI STORED PROCEDURE...");

                    var pUserId = new SqlParameter("@UserID", userId);
                    var pStoryId = new SqlParameter("@StoryID", chapter.StoryId);
                    var pChapterId = new SqlParameter("@ChapterID", id);

                    int rows = await _context.Database.ExecuteSqlRawAsync(
                        "EXEC [dbo].[sp_SaveUserReadingProgress] @UserID, @StoryID, @ChapterID",
                        pUserId, pStoryId, pChapterId
                    );

                    Console.WriteLine($"===================> THỰC THI SP THÀNH CÔNG! Số dòng ảnh hưởng: {rows}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"===================> LỖI SP: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("===================> KẾT QUẢ: UserId = 0 NÊN KHÔNG GỌI STORED PROCEDURE!");
            }

            return View(chapter);
        }
    }
}
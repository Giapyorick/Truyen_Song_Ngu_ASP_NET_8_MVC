using WebTruyenTranh.Helpers;
using Microsoft.AspNetCore.Mvc;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace WebTruyenTranh.Components
{
    [ViewComponent(Name = "ChaptersList")]
    public class ChaptersListComponent : ViewComponent
    {
        private readonly TruyenSongNguContext _context;
        public ChaptersListComponent(TruyenSongNguContext context)
        {
            _context = context;
        }
        public async Task<IViewComponentResult> InvokeAsync(int storyId, int page = 1)
        {
            int pageSize = 10; 
            if (page < 1) page = 1;

            int totalChapters = await _context.TblChapters.CountAsync(c => c.StoryId == storyId);
            int totalPages = (int)Math.Ceiling((double)totalChapters / pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var chapters = await _context.TblChapters
                .Where(c => c.StoryId == storyId)
                .OrderBy(c => c.ChapterNumber) 
                .Skip((page - 1) * pageSize)   
                .Take(pageSize)                
                .Select(c => new ChaptersListViewModel
                {
                    ChapterId = c.ChapterId,
                    ChapterNumber = c.ChapterNumber,
                    Title = c.Title,
                    CreatedDate = c.CreatedDate
                })
                .ToListAsync();

            var model = new ChaptersPagedViewModel
            {
                Chapters = chapters,
                CurrentPage = page,
                TotalPages = totalPages,
                StoryId = storyId
            };

            return await Task.FromResult((IViewComponentResult)View("ChaptersList", model));
        }
    }
}

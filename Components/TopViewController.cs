using Microsoft.AspNetCore.Mvc;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace WebTruyenTranh.Components
{

    [ViewComponent(Name = "TopViewList")]
    public class TopViewListComponent : ViewComponent
    {
        private readonly TruyenSongNguContext _context;
        public TopViewListComponent(TruyenSongNguContext context)
        {
            _context = context;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var topStories = await _context.TblStories
                .Include(s => s.Author)
                .OrderByDescending(s => s.Likes)
                .Select(s => new TopViewListViewModel
                {
                    StoryId = s.StoryId,
                    Title = s.Title,
                    Likes = s.Likes,
                    img = s.Img,
                    author = s.Author.AuthorName ?? "Unknown"
                })
                .Take(10)
                .ToListAsync();
                

            return await Task.FromResult((IViewComponentResult)View("TopViewList", topStories));
        }
    }
}

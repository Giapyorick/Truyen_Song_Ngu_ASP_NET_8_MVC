using WebTruyenTranh.Helpers;
using Microsoft.AspNetCore.Mvc;
using WebTruyenTranh.Models;
using WebTruyenTranh.ViewModels;
namespace WebTruyenTranh.Components
{
	[ViewComponent(Name = "StoriesList")]
	public class StroriesListComponent: ViewComponent{
		private readonly TruyenSongNguContext _context;
		public StroriesListComponent(TruyenSongNguContext context){
			_context = context;
		}
		public async Task<IViewComponentResult> InvokeAsync(){
			int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            var stories = _context.TblStories
                .Select(s => new StoryListViewModel
                {
                    StoryID = s.StoryId,
                    Title = s.Title,
                    Img = s.Img,
                    Likes = s.Likes,
                    Rate = s.Rate,
                    CountFolower = s.CountFolower,
                    CountRate = s.CountRate,
                    HasProgress = _context.TblUserReadingProgresses
                        .Any(p => p.UserId == userId && p.StoryId == s.StoryId),

                    LastChapterId = _context.TblUserReadingProgresses
                        .Where(p => p.UserId == userId && p.StoryId == s.StoryId)
                        .Select(p => (int?)p.LastChapterId)
                        .FirstOrDefault(),
                    LastChapterNumber = (
                        from p in _context.TblUserReadingProgresses
                        join c in _context.TblChapters on p.LastChapterId equals c.ChapterId
                        where p.UserId == userId && p.StoryId == s.StoryId
                        select (int?)c.ChapterNumber
                    ).FirstOrDefault()
                })
                .ToList();

            return await Task.FromResult((IViewComponentResult)View("StoriesList", stories));
		}
	}
	
}
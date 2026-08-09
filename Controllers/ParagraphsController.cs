using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebTruyenTranh.Models;

public class ParagraphsController : Controller
{
    private readonly TruyenSongNguContext _context;

    public ParagraphsController(TruyenSongNguContext context)
    {
        _context = context;
    }

    public IActionResult Content(int chapterId)
    {
        ViewBag.ChapterId = chapterId;
        return View();
    }

    [HttpGet]
    public IActionResult GetByChapter(int chapterId)
    {
        var data = _context.TblParagraphs
            .Where(x => x.ChapterId == chapterId)
            .OrderBy(x => x.ParagraphOrder)
            .Select(x => new
            {
                x.ParagraphId,
                English = x.English ?? "",
                Vietnamese = x.Vietnamese ?? "",
                Chinese = x.Chinese ?? "",
                Japanese = x.Japanese ?? "",
                French = x.French ?? ""
            })
            .ToList();

        return Json(data);
    }
    [HttpGet]
    public IActionResult GetListByChapter(int chapterId)
    {
        var currentChapter = _context.TblChapters
            .Include(c => c.Story)
            .FirstOrDefault(c => c.ChapterId == chapterId);

        if (currentChapter == null)
        {
            return NotFound(new { message = "Không tìm thấy chương hiện tại." });
        }

        var chaptersList = _context.TblChapters
            .Where(c => c.StoryId == currentChapter.StoryId)
            .OrderBy(c => c.ChapterNumber)
            .Select(c => new
            {
                c.ChapterId,
                c.ChapterNumber,
                c.Title
            })
            .ToList();

        return Json(new
        {
            storyTitle = currentChapter.Story?.Title ?? "Truyện Song Ngữ",
            currentChapterTitle = $"Chương {currentChapter.ChapterNumber}: {currentChapter.Title}",
            chapters = chaptersList
        });
    }

}

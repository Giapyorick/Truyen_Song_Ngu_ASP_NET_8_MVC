using Microsoft.AspNetCore.Mvc;
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
                x.English,
                x.Vietnamese
            })
            .ToList();

        return Json(data);
    }
}

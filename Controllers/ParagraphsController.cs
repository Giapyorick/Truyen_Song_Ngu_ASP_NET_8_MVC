using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebTruyenTranh.Models;

public class ParagraphsController : Controller
{
    private readonly TruyenSongNguContext _context;

    public ParagraphsController(TruyenSongNguContext context)
    {
        _context = context;
    }

    // Chuyển sang async Task<IActionResult> để thực thi Stored Procedure bất đồng bộ
    public async Task<IActionResult> Content(int chapterId)
    {
        ViewBag.ChapterId = chapterId;
        int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

        Console.WriteLine($"===================> PARAGRAPHS CONTENT: UserId = {userId}, ChapterId = {chapterId}");

        if (userId > 0)
        {
            var chapter = await _context.TblChapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);

            if (chapter != null)
            {
                try
                {
                    var pUserId = new SqlParameter("@UserID", userId);
                    var pStoryId = new SqlParameter("@StoryID", chapter.StoryId);
                    var pChapterId = new SqlParameter("@ChapterID", chapterId);

                    await _context.Database.ExecuteSqlRawAsync(
                        "EXEC [dbo].[sp_SaveUserReadingProgress] @UserID, @StoryID, @ChapterID",
                        pUserId, pStoryId, pChapterId
                    );

                    Console.WriteLine($"[SUCCESS] Luu tien trinh thanh cong cho User {userId}!");
                }
                catch (Microsoft.Data.SqlClient.SqlException sqlEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR SQL SERVER CODE {sqlEx.Number}]: {sqlEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR]: {ex.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine("===================> SKIP SAVING PROGRESS: UserId = 0 (Not signed in)");
        }

        return View();
    }
    [HttpPost]
    public async Task<IActionResult> UpdateProgress(int chapterId)
    {
        int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

        if (userId > 0)
        {
            var chapter = await _context.TblChapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId);
            if (chapter != null)
            {
                try
                {
                    var pUserId = new Microsoft.Data.SqlClient.SqlParameter("@UserID", userId);
                    var pStoryId = new Microsoft.Data.SqlClient.SqlParameter("@StoryID", chapter.StoryId);
                    var pChapterId = new Microsoft.Data.SqlClient.SqlParameter("@ChapterID", chapterId);

                    await _context.Database.ExecuteSqlRawAsync(
                        "EXEC [dbo].[sp_SaveUserReadingProgress] @UserID, @StoryID, @ChapterID",
                        pUserId, pStoryId, pChapterId
                    );

                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = ex.Message });
                }
            }
        }

        return Json(new { success = false, message = "Not signed in or chapter does not exist!" });
    }

    [HttpGet]
    public IActionResult GetByChapter(int chapterId)
    {
        var data = _context.TblParagraphs
            .AsNoTracking()
            .Where(x => x.ChapterId == chapterId)
            .OrderBy(x => x.ParagraphOrder)
            .Select(x => new
            {
                paragraphId = x.ParagraphId,
                paragraphOrder = x.ParagraphOrder,
                blockType = x.BlockType ?? 1, 
                english = x.English ?? "",
                vietnamese = x.Vietnamese ?? "",
                chinese = x.Chinese ?? "",
                japanese = x.Japanese ?? "",
                french = x.French ?? ""
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
            return NotFound(new { message = "Chapter not found." });
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
            storyTitle = currentChapter.Story?.Title ?? "Bilingual Story",
            currentChapterTitle = $"Chapter {currentChapter.ChapterNumber}: {currentChapter.Title}",
            chapters = chaptersList
        });
    }
}
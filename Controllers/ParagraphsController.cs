using System;
using System.Globalization;
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

    public async Task<IActionResult> Content(int chapterId)
    {
        ViewBag.ChapterId = chapterId;
        int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

        if (userId > 0)
        {
            var chapter = await _context.TblChapters.AsNoTracking().FirstOrDefaultAsync(c => c.ChapterId == chapterId);
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
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR SAVE PROGRESS]: {ex.Message}");
                }
            }
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProgress(int chapterId)
    {
        int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
        if (userId > 0)
        {
            var chapter = await _context.TblChapters.AsNoTracking().FirstOrDefaultAsync(c => c.ChapterId == chapterId);
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

    // 1. API LẤY NỘI DUNG ĐOẠN VĂN
    [HttpGet]
    public async Task<IActionResult> GetByChapter(int chapterId)
    {
        try
        {
            var data = await _context.TblParagraphs
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
                .ToListAsync();

            return Json(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    // 2. API LẤY DANH SÁCH CHƯƠNG VÀ TIÊU ĐỀ SONG NGỮ
    [HttpGet]
    public async Task<IActionResult> GetListByChapter(int chapterId)
    {
        try
        {
            var currentCulture = CultureInfo.CurrentUICulture.Name;
            bool isVi = currentCulture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
            string langPrefix = isVi ? "vi" : "en";
            string chapterPrefix = isVi ? "Chương" : "Chapter";

            // Bước 1: Lấy chapter hiện tại
            var currentChapter = await _context.TblChapters
                .AsNoTracking()
                .Where(c => c.ChapterId == chapterId)
                .Select(c => new { c.ChapterId, c.ChapterNumber, c.StoryId, c.Title })
                .FirstOrDefaultAsync();

            if (currentChapter == null)
            {
                return NotFound(new { message = "Chapter not found." });
            }

            int storyId = currentChapter.StoryId;

            // Bước 2: Lấy tiêu đề truyện theo ngôn ngữ
            var story = await _context.TblStories
                .AsNoTracking()
                .Where(s => s.StoryId == storyId)
                .Select(s => new
                {
                    DefaultTitle = s.Title,
                    TransTitle = s.TblStoryTranslations
                                  .Where(t => t.LanguageCode.StartsWith(langPrefix))
                                  .Select(t => t.Title)
                                  .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            string storyTitle = story?.TransTitle ?? story?.DefaultTitle ?? "Bilingual Story";

            // Bước 3: Lấy toàn bộ chapter của truyện kèm tiêu đề dịch
            var chapters = await _context.TblChapters
                .AsNoTracking()
                .Where(c => c.StoryId == storyId)
                .OrderBy(c => c.ChapterNumber)
                .Select(c => new
                {
                    ChapterId = c.ChapterId,
                    ChapterNumber = c.ChapterNumber,
                    DefaultTitle = c.Title,
                    TransTitle = c.TblChapterTranslations
                                  .Where(ct => ct.LanguageCode.StartsWith(langPrefix))
                                  .Select(ct => ct.Title)
                                  .FirstOrDefault()
                })
                .ToListAsync();

            var formattedChapters = chapters.Select(c =>
            {
                string t = !string.IsNullOrWhiteSpace(c.TransTitle) ? c.TransTitle : c.DefaultTitle;
                return new
                {
                    chapterId = c.ChapterId,
                    chapterNumber = c.ChapterNumber,
                    title = string.IsNullOrWhiteSpace(t) ? $"{chapterPrefix} {c.ChapterNumber}" : $"{chapterPrefix} {c.ChapterNumber}: {t}"
                };
            }).ToList();

            var cur = chapters.FirstOrDefault(c => c.ChapterId == chapterId);
            string curTitle = cur != null && !string.IsNullOrWhiteSpace(cur.TransTitle) ? cur.TransTitle : currentChapter.Title;
            string fullCurTitle = string.IsNullOrWhiteSpace(curTitle)
                ? $"{chapterPrefix} {currentChapter.ChapterNumber}"
                : $"{chapterPrefix} {currentChapter.ChapterNumber}: {curTitle}";

            return Json(new
            {
                storyId = storyId,
                storyTitle = storyTitle,
                currentChapterTitle = fullCurTitle,
                chapterNumber = currentChapter.ChapterNumber,
                chapters = formattedChapters
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
        }
    }
}
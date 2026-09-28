using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
[Area("Admin")]
public class MangaTranslateController : Controller
{
    private readonly MangaTranslatorService _translatorService;

    // Inject Service thông qua Constructor
    public MangaTranslateController(MangaTranslatorService translatorService)
    {
        _translatorService = translatorService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // Allow GET so direct navigation won't return 405. Keep POST for the actual upload.
    [HttpGet]
    public IActionResult Translate()
    {
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Route("Admin/MangaTranslate/Translate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Translate(IFormFile mangaFile, string maTruyen, string maChuong, int maTrang)
    {
        if (mangaFile == null || mangaFile.Length == 0)
        {
            ViewBag.Error = "Vui lòng chọn file ảnh trang truyện!";
            return View("Index");
        }

        try
        {
            // 1. Gọi Service gửi ảnh qua Python API
            byte[] translatedBytes = await _translatorService.TranslateImageAsync(mangaFile);

            // 2. Tạo tên file chuẩn: manga_matruyen_machuong_matrang_random.jpg
            string randomStr = Guid.NewGuid().ToString("N").Substring(0, 6);
            string fileName = $"manga_{maTruyen}_{maChuong}_{maTrang}_{randomStr}.jpg";

            // 3. Tạo đường dẫn lưu vào wwwroot/assets/image/
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "image");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string physicalPath = Path.Combine(folderPath, fileName);

            // 4. Lưu file ra đĩa
            await System.IO.File.WriteAllBytesAsync(physicalPath, translatedBytes);

            // 5. Đường dẫn tương đối dùng hiển thị ở View hoặc lưu DB
            string relativeUrl = $"/assets/image/{fileName}";
            ViewBag.ImageUrl = relativeUrl;
            ViewBag.Message = "Dịch & Lưu ảnh vào Assets thành công!";

            return View("Index");
        }
        catch (Exception ex)
        {
            ViewBag.Error = "Xảy ra lỗi: " + ex.Message;
            return View("Index");
        }
    }

    // =========================================================================
    // HÀM DỌN DẸP / XÓA ẢNH (Chuyển thành private/protected để tránh bị expose làm Action)
    // =========================================================================

    // Xóa 1 trang ảnh
    [NonAction]
    public bool DeletePageImage(string relativePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(relativePath))
            {
                // Cải tiến: Xóa cả '/' và '\' ở đầu chuỗi
                string cleanPath = relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", cleanPath);

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    // Xóa toàn bộ ảnh của 1 chương
    [NonAction]
    public void DeleteImagesByChapter(string maTruyen, string maChuong)
    {
        string pattern = $"manga_{maTruyen}_{maChuong}_*.jpg";
        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "image");

        if (Directory.Exists(folderPath))
        {
            foreach (var file in Directory.GetFiles(folderPath, pattern))
            {
                if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
            }
        }
    }

    // Xóa toàn bộ ảnh của 1 truyện
    [NonAction]
    public void DeleteImagesByManga(string maTruyen)
    {
        string pattern = $"manga_{maTruyen}_*.jpg";
        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "image");

        if (Directory.Exists(folderPath))
        {
            foreach (var file in Directory.GetFiles(folderPath, pattern))
            {
                if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
            }
        }
    }
}
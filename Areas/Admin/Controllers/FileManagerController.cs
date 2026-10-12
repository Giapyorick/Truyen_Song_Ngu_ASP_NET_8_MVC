using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class FileManagerController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public FileManagerController(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        private string GetAdminUploadFolder()
        {
            // Lưu tệp riêng biệt trong thư mục Admin
            string folder = Path.Combine(_webHostEnvironment.WebRootPath, "assets", "template");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return folder;
        }

        [HttpPost]
        public async Task<IActionResult> UploadFiles(IFormFileCollection files)
        {
            if (files == null || files.Count == 0)
                return Json(new { success = false, message = "No files selected!" });

            string uploadFolder = GetAdminUploadFolder();

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    string fileName = Path.GetFileName(file.FileName);
                    string filePath = Path.Combine(uploadFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                }
            }

            return Json(new { success = true, message = "Files uploaded successfully!" });
        }

        [HttpGet]
        public IActionResult GetSavedFiles()
        {
            string uploadFolder = GetAdminUploadFolder();
            var dir = new DirectoryInfo(uploadFolder);

            var files = dir.GetFiles()
                .OrderByDescending(f => f.CreationTime)
                .Select(f => new
                {
                    fileName = f.Name,
                    fileSize = (f.Length / 1024.0).ToString("0.0") + " KB",
                    createdDate = f.CreationTime.ToString("dd/MM/yyyy HH:mm"),
                    extension = f.Extension.ToLower()
                }).ToList();

            return Json(new { success = true, data = files });
        }

        [HttpGet]
        public IActionResult DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            string filePath = Path.Combine(GetAdminUploadFolder(), fileName);
            if (!System.IO.File.Exists(filePath)) return NotFound();

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/octet-stream", fileName);
        }

        [HttpPost]
        public IActionResult DeleteFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return Json(new { success = false, message = "Invalid file name!" });

            string filePath = Path.Combine(GetAdminUploadFolder(), fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                return Json(new { success = true, message = "File deleted successfully!" });
            }

            return Json(new { success = false, message = "File not found!" });
        }
    }
}
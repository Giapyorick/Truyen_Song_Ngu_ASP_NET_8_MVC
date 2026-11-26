using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.Services;

namespace WebTruyenTranh.Areas.Admin.Controllers
{
    /// <summary>
    /// Admin > Audio truyện: admin tự chọn truyện / chương / ngôn ngữ / model / giọng để tạo audio ElevenLabs.
    /// Tạo audio chạy nền bằng Hangfire (Helpers/ParagraphAudioService.cs). File: wwwroot/audio/...
    /// </summary>
    [Area("Admin")]
    [AdminAuthorize]
    public class AudioController : Controller
    {
        private const string RunningJobCacheKey = "audio-job:running";

        private readonly TruyenSongNguContext _context;
        private readonly IElevenLabsService _elevenLabs;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;

        public AudioController(TruyenSongNguContext context, IElevenLabsService elevenLabs, IWebHostEnvironment env, IMemoryCache cache)
        {
            _context = context;
            _elevenLabs = elevenLabs;
            _env = env;
            _cache = cache;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.ElevenLabsConfigured = _elevenLabs.IsConfigured;
            return View();
        }

        /// <summary>Model + hạn mức ký tự còn lại</summary>
        [HttpGet]
        public async Task<IActionResult> Setup()
        {
            var models = ElevenLabsService.Models.Select(m => new
            {
                id = m.Id,
                name = m.Name,
                note = m.Note,
                languages = m.Languages,
                creditPerChar = m.CreditPerChar
            });

            object? quota = null;
            string? quotaError = null;
            if (_elevenLabs.IsConfigured)
            {
                try
                {
                    var q = await _elevenLabs.GetQuotaAsync(HttpContext.RequestAborted);
                    if (q != null) quota = new { used = q.Used, limit = q.Limit, remaining = q.Remaining, resetAt = q.ResetAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") };
                }
                catch (Exception ex) { quotaError = ex.Message; }
            }

            return Json(new { success = true, configured = _elevenLabs.IsConfigured, models, quota, quotaError, runningJob = GetRunningJob() });
        }

        /// <summary>Giọng đọc trong tài khoản ElevenLabs (cache 10 phút)</summary>
        [HttpGet]
        public async Task<IActionResult> Voices(bool refresh = false)
        {
            if (!_elevenLabs.IsConfigured)
                return Json(new { success = false, message = "Chưa cấu hình ElevenLabs:ApiKey." });

            try
            {
                const string key = "elevenlabs:voices";
                if (refresh || !_cache.TryGetValue(key, out List<ElevenLabsVoice>? voices) || voices == null)
                {
                    voices = await _elevenLabs.GetVoicesAsync(HttpContext.RequestAborted);
                    _cache.Set(key, voices, TimeSpan.FromMinutes(10));
                }

                return Json(new
                {
                    success = true,
                    voices = voices.Select(v => new { id = v.VoiceId, name = v.Name, category = v.Category, description = v.Description, previewUrl = v.PreviewUrl })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>Thống kê audio theo chương của truyện cho 1 ngôn ngữ</summary>
        [HttpGet]
        public async Task<IActionResult> ChapterStatus(int storyId, string lang, bool overwrite = false)
        {
            string? code = ParagraphAudioHelper.NormalizeLang(lang);
            if (storyId <= 0 || code == null) return Json(new { success = false, message = "Chọn truyện và ngôn ngữ." });

            var chapters = await ParagraphAudioHelper.ComputeStatusAsync(_context, storyId, code, overwrite);
            return Json(new
            {
                success = true,
                lang = code,
                chapters = chapters.Select(c => new
                {
                    chapterId = c.ChapterId,
                    chapterNumber = c.ChapterNumber,
                    title = c.Title,
                    total = c.Total,
                    done = c.Done,
                    outdated = c.Outdated,
                    missing = c.Missing,
                    toGenerate = c.ToGenerate,
                    charsToGenerate = c.CharsToGenerate,
                    voices = c.Voices
                })
            });
        }

        public class StartAudioRequest
        {
            public int StoryId { get; set; }
            public List<int> ChapterIds { get; set; } = new();
            public string Lang { get; set; } = "";
            public string ModelId { get; set; } = "";
            public string VoiceId { get; set; } = "";
            public string VoiceName { get; set; } = "";
            public bool Overwrite { get; set; }
        }

        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Start([FromBody] StartAudioRequest req)
        {
            if (!_elevenLabs.IsConfigured)
                return Json(new { success = false, message = "Chưa cấu hình ElevenLabs:ApiKey." });

            string? lang = ParagraphAudioHelper.NormalizeLang(req.Lang);
            var model = ElevenLabsService.FindModel(req.ModelId);
            if (req.StoryId <= 0 || lang == null) return Json(new { success = false, message = "Chọn truyện và ngôn ngữ." });
            if (model == null) return Json(new { success = false, message = "Chọn model." });
            if (!model.Languages.Contains(lang)) return Json(new { success = false, message = $"{model.Name} không đọc được ngôn ngữ này. Hãy chọn model khác." });
            if (string.IsNullOrWhiteSpace(req.VoiceId)) return Json(new { success = false, message = "Chọn giọng đọc." });
            if (req.ChapterIds == null || req.ChapterIds.Count == 0) return Json(new { success = false, message = "Chọn ít nhất 1 chương." });

            // Chỉ chạy 1 job audio một lúc
            var running = GetRunningJob();
            if (running != null)
                return Json(new { success = false, message = "Đang có một lượt tạo audio chạy. Hãy chờ xong hoặc bấm Dừng." });

            var status = await ParagraphAudioHelper.ComputeStatusAsync(_context, req.StoryId, lang, req.Overwrite, req.ChapterIds);
            int count = status.Sum(s => s.ToGenerate);
            long chars = status.Sum(s => s.CharsToGenerate);
            if (count == 0) return Json(new { success = false, message = "Các chương đã chọn đều đã có audio (bật \"Tạo lại\" nếu muốn tạo lại)." });

            // Kiểm tra hạn mức trước khi chạy
            try
            {
                var quota = await _elevenLabs.GetQuotaAsync(HttpContext.RequestAborted);
                long credits = (long)Math.Ceiling(chars * model.CreditPerChar);
                if (quota != null && quota.Limit > 0 && credits > quota.Remaining)
                    return Json(new { success = false, message = $"Cần khoảng {credits:N0} credit nhưng tài khoản chỉ còn {quota.Remaining:N0}. Hãy chọn ít chương hơn." });
            }
            catch (ElevenLabsException ex) when (ex.IsFatal)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch { /* không lấy được hạn mức thì vẫn cho chạy */ }

            var jobRequest = new AudioJobRequest
            {
                StoryId = req.StoryId,
                ChapterIds = req.ChapterIds.Distinct().ToList(),
                Lang = lang,
                ModelId = model.Id,
                VoiceId = req.VoiceId.Trim(),
                VoiceName = req.VoiceName ?? "",
                Overwrite = req.Overwrite,
                ProgressKey = Guid.NewGuid().ToString("N")
            };

            var jobId = BackgroundJob.Enqueue<IParagraphAudioService>(s => s.RunAudioJob(jobRequest, JobCancellationToken.Null));
            _cache.Set(RunningJobCacheKey, new RunningJob { JobId = jobId, ProgressKey = jobRequest.ProgressKey, StoryId = req.StoryId, Lang = lang }, TimeSpan.FromDays(1));

            return Json(new
            {
                success = true,
                jobId,
                progressKey = jobRequest.ProgressKey,
                message = $"Đã bắt đầu tạo audio cho {count:N0} câu ({chars:N0} ký tự)."
            });
        }

        /// <summary>Tiến độ job</summary>
        [HttpGet]
        public IActionResult Status(string jobId, string progressKey)
        {
            var progress = _cache.Get<AudioJobProgress>(ParagraphAudioService.CacheKey(progressKey ?? ""));
            string? state = null, error = null;

            if (!string.IsNullOrWhiteSpace(jobId))
            {
                var details = JobStorage.Current.GetMonitoringApi().JobDetails(jobId);
                var last = details?.History.FirstOrDefault();
                state = last?.StateName;
                if (state == "Failed" && last?.Data != null && last.Data.TryGetValue("ExceptionMessage", out var m)) error = m;
            }

            bool completed = state is "Succeeded" or "Failed" or "Deleted" || (state == null && progress?.Finished == true);
            if (completed) ClearRunningJob(jobId);

            return Json(new
            {
                success = true,
                state,
                completed,
                error = error ?? (state == "Failed" ? progress?.LastError : null),
                progress = progress == null ? null : new
                {
                    total = progress.Total,
                    done = progress.Done,
                    failed = progress.Failed,
                    chars = progress.Chars,
                    currentChapter = progress.CurrentChapter,
                    lastError = progress.LastError,
                    cancelled = progress.Cancelled || state == "Deleted",
                    storyId = progress.StoryId,
                    lang = progress.Lang
                }
            });
        }

        /// <summary>Dừng job (câu đã tạo vẫn được giữ)</summary>
        [HttpPost]
        [AdminRoleAuthorize]
        public IActionResult Stop(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return Json(new { success = false, message = "Thiếu jobId." });
            bool ok = BackgroundJob.Delete(jobId);
            return Json(new { success = ok, message = ok ? "Đang dừng... Các câu đã tạo vẫn được giữ lại." : "Không dừng được (job có thể đã xong)." });
        }

        public class DeleteAudioRequest
        {
            public int StoryId { get; set; }
            public List<int> ChapterIds { get; set; } = new();
            public string Lang { get; set; } = "";
        }

        /// <summary>Xóa audio (file + dữ liệu) của các chương đã chọn theo 1 ngôn ngữ</summary>
        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> Delete([FromBody] DeleteAudioRequest req)
        {
            string? lang = ParagraphAudioHelper.NormalizeLang(req.Lang);
            if (req.StoryId <= 0 || lang == null || req.ChapterIds == null || req.ChapterIds.Count == 0)
                return Json(new { success = false, message = "Chọn truyện, ngôn ngữ và ít nhất 1 chương." });
            if (GetRunningJob() != null)
                return Json(new { success = false, message = "Đang tạo audio, hãy chờ xong hoặc dừng trước khi xóa." });

            var rows = await ParagraphAudioHelper.LoadExistingAsync(_context, req.StoryId, lang, req.ChapterIds, tracking: true);
            int files = 0;
            foreach (var row in rows.Values)
            {
                var path = ParagraphAudioHelper.PhysicalPath(_env.WebRootPath, row.FilePath);
                if (path != null && System.IO.File.Exists(path))
                {
                    try { System.IO.File.Delete(path); files++; } catch { }
                }
            }
            _context.TblParagraphAudios.RemoveRange(rows.Values);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã xóa {rows.Count:N0} audio ({files:N0} file)." });
        }

        /// <summary>
        /// Dọn file mp3 không còn dữ liệu (câu/chương/truyện đã bị xóa) trong wwwroot/audio.
        /// </summary>
        [HttpPost]
        [AdminRoleAuthorize]
        public async Task<IActionResult> CleanupOrphans()
        {
            if (GetRunningJob() != null)
                return Json(new { success = false, message = "Đang tạo audio, hãy chờ xong rồi dọn." });

            var root = Path.Combine(_env.WebRootPath, "audio");
            if (!Directory.Exists(root)) return Json(new { success = true, message = "Chưa có file audio nào." });

            var known = (await _context.TblParagraphAudios.AsNoTracking().Select(a => a.FilePath).ToListAsync())
                .Select(p => ParagraphAudioHelper.PhysicalPath(_env.WebRootPath, p))
                .Where(p => p != null)
                .Select(p => p!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int deleted = 0;
            long bytes = 0;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                bool orphan = file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                              (file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) && !known.Contains(Path.GetFullPath(file)));
                if (!orphan) continue;
                try
                {
                    bytes += new FileInfo(file).Length;
                    System.IO.File.Delete(file);
                    deleted++;
                }
                catch { }
            }

            // Xóa thư mục rỗng
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                try { if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); } catch { }
            }

            return Json(new { success = true, message = deleted == 0 ? "Không có file thừa." : $"Đã xóa {deleted:N0} file thừa ({bytes / 1024.0 / 1024.0:N1} MB)." });
        }

        // ---------------------------------------------------------------------
        private class RunningJob
        {
            public string JobId { get; set; } = "";
            public string ProgressKey { get; set; } = "";
            public int StoryId { get; set; }
            public string Lang { get; set; } = "";
        }

        /// <summary>Job audio đang chạy (null nếu không có hoặc đã kết thúc)</summary>
        private object? GetRunningJob()
        {
            if (!_cache.TryGetValue(RunningJobCacheKey, out RunningJob? job) || job == null) return null;

            var state = JobStorage.Current.GetMonitoringApi().JobDetails(job.JobId)?.History.FirstOrDefault()?.StateName;
            if (state is null or "Succeeded" or "Failed" or "Deleted")
            {
                _cache.Remove(RunningJobCacheKey);
                return null;
            }
            return new { jobId = job.JobId, progressKey = job.ProgressKey, storyId = job.StoryId, lang = job.Lang };
        }

        private void ClearRunningJob(string jobId)
        {
            if (_cache.TryGetValue(RunningJobCacheKey, out RunningJob? job) && job?.JobId == jobId)
                _cache.Remove(RunningJobCacheKey);
        }
    }
}

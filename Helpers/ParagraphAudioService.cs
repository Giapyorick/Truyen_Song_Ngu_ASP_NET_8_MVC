using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.Services
{
    // =========================================================================
    // DỮ LIỆU JOB
    // =========================================================================
    /// <summary>Yêu cầu tạo audio (Hangfire lưu dạng JSON nên chỉ dùng kiểu đơn giản)</summary>
    public class AudioJobRequest
    {
        public int StoryId { get; set; }
        /// <summary>Danh sách chương cần tạo (rỗng = cả truyện)</summary>
        public List<int> ChapterIds { get; set; } = new();
        /// <summary>en | vi | zh | ja | fr</summary>
        public string Lang { get; set; } = "en";
        public string ModelId { get; set; } = "";
        public string VoiceId { get; set; } = "";
        public string VoiceName { get; set; } = "";
        /// <summary>true = tạo lại cả câu đã có audio</summary>
        public bool Overwrite { get; set; }
        /// <summary>Khóa tiến độ trong bộ nhớ (controller tạo trước khi đưa vào hàng đợi)</summary>
        public string ProgressKey { get; set; } = "";
    }

    /// <summary>Tiến độ job, giữ trong IMemoryCache (Hangfire chạy cùng tiến trình web)</summary>
    public class AudioJobProgress
    {
        public int StoryId { get; set; }
        public string Lang { get; set; } = "";
        public int Total { get; set; }
        public int Done { get; set; }
        public int Failed { get; set; }
        public long Chars { get; set; }
        public string? CurrentChapter { get; set; }
        public string? LastError { get; set; }
        public bool Finished { get; set; }
        public bool Cancelled { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Thống kê audio của 1 chương theo 1 ngôn ngữ</summary>
    public class ChapterAudioStatus
    {
        public int ChapterId { get; set; }
        public int ChapterNumber { get; set; }
        public string Title { get; set; } = "";
        /// <summary>Số câu có nội dung để đọc</summary>
        public int Total { get; set; }
        /// <summary>Có audio, nội dung chưa đổi</summary>
        public int Done { get; set; }
        /// <summary>Có audio nhưng câu đã bị sửa sau đó</summary>
        public int Outdated { get; set; }
        public int Missing => Math.Max(0, Total - Done - Outdated);
        /// <summary>Số câu / ký tự sẽ tạo nếu bấm "Tạo audio" (tùy chọn tạo lại)</summary>
        public int ToGenerate { get; set; }
        public long CharsToGenerate { get; set; }
        /// <summary>Giọng / model của audio đã có (để hiển thị)</summary>
        public List<string> Voices { get; set; } = new();
    }

    // =========================================================================
    // HÀM DÙNG CHUNG (controller + job)
    // =========================================================================
    public static class ParagraphAudioHelper
    {
        public static readonly string[] Languages = { "en", "vi", "zh", "ja", "fr" };

        /// <summary>"Vietnamese" / "vietnamese" / "vi" / "Tiếng Việt" -> "vi"</summary>
        public static string? NormalizeLang(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return null;
            string l = lang.Trim().ToLowerInvariant();
            if (Languages.Contains(l)) return l;
            return ParagraphImportParser.NormalizeLanguage(lang) switch
            {
                ParagraphImportParser.English => "en",
                ParagraphImportParser.Vietnamese => "vi",
                ParagraphImportParser.Chinese => "zh",
                ParagraphImportParser.Japanese => "ja",
                ParagraphImportParser.French => "fr",
                _ => null
            };
        }

        private static readonly Regex MediaTag = new(@"\[(?:img|strip|multistrip)\b[^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HtmlTag = new(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>Bỏ thẻ ảnh [img ...], thẻ HTML, giải mã &amp;... -> văn bản thuần để đọc</summary>
        public static string CleanText(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = MediaTag.Replace(raw, " ");
            // <br>, </p>, </div> -> khoảng trắng; thẻ định dạng (<i>, <b>...) -> bỏ hẳn để không chen khoảng trắng trước dấu câu
            s = Regex.Replace(s, @"<br\s*/?>|</(?:p|div|li)>", " ", RegexOptions.IgnoreCase);
            s = HtmlTag.Replace(s, "");
            s = WebUtility.HtmlDecode(s);
            s = Spaces.Replace(s, " ").Trim();
            // Không có chữ / số nào (chỉ dấu câu, ký hiệu) -> không đọc
            return s.Any(char.IsLetterOrDigit) ? s : "";
        }

        public static string Hash(string text)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
        }

        public static string WebPath(int storyId, int chapterId, int paragraphId, string lang) =>
            $"/audio/s{storyId}/c{chapterId}/p{paragraphId}_{lang}.mp3";

        /// <summary>Đường dẫn web -> đường dẫn file (chặn mọi đường dẫn ra ngoài wwwroot/audio)</summary>
        public static string? PhysicalPath(string webRootPath, string? webPath)
        {
            if (string.IsNullOrWhiteSpace(webPath) || !webPath.StartsWith("/audio/")) return null;
            var root = Path.GetFullPath(Path.Combine(webRootPath, "audio"));
            var full = Path.GetFullPath(Path.Combine(webRootPath, webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
        }

        /// <summary>Câu này có cần tạo audio không</summary>
        public static bool NeedsAudio(TblParagraphAudio? existing, string hash, bool overwrite) =>
            overwrite || existing == null || existing.TextHash != hash;

        /// <summary>Cắt câu quá dài theo dấu câu để không vượt giới hạn ký tự của model</summary>
        public static List<string> SplitForTts(string text, int maxChars)
        {
            var parts = new List<string>();
            if (text.Length <= maxChars) { parts.Add(text); return parts; }

            var sentences = Regex.Split(text, @"(?<=[\.!\?。！？…;；])\s*");
            var sb = new StringBuilder();
            foreach (var raw in sentences)
            {
                var s = raw;
                while (s.Length > maxChars)
                {
                    if (sb.Length > 0) { parts.Add(sb.ToString().Trim()); sb.Clear(); }
                    int cut = s.LastIndexOf(' ', maxChars);
                    if (cut < maxChars / 2) cut = maxChars;
                    parts.Add(s.Substring(0, cut).Trim());
                    s = s.Substring(cut);
                }
                if (sb.Length + s.Length + 1 > maxChars && sb.Length > 0) { parts.Add(sb.ToString().Trim()); sb.Clear(); }
                sb.Append(s).Append(' ');
            }
            if (sb.ToString().Trim().Length > 0) parts.Add(sb.ToString().Trim());
            return parts.Where(p => p.Length > 0).ToList();
        }

        public class ParagraphText
        {
            public int ParagraphId { get; set; }
            public int ChapterId { get; set; }
            public int ParagraphOrder { get; set; }
            public string? Raw { get; set; }
        }

        /// <summary>Lấy nội dung 1 ngôn ngữ của các câu thuộc truyện (lọc theo chương nếu có)</summary>
        public static IQueryable<ParagraphText> QueryTexts(TruyenSongNguContext context, int storyId, string lang, IReadOnlyCollection<int>? chapterIds)
        {
            var q = context.TblParagraphs.AsNoTracking().Where(p => p.Chap.StoryId == storyId);
            if (chapterIds != null && chapterIds.Count > 0) q = q.Where(p => chapterIds.Contains(p.ChapterId));

            return lang switch
            {
                "vi" => q.Select(p => new ParagraphText { ParagraphId = p.ParagraphId, ChapterId = p.ChapterId, ParagraphOrder = p.ParagraphOrder, Raw = p.Vietnamese }),
                "zh" => q.Select(p => new ParagraphText { ParagraphId = p.ParagraphId, ChapterId = p.ChapterId, ParagraphOrder = p.ParagraphOrder, Raw = p.Chinese }),
                "ja" => q.Select(p => new ParagraphText { ParagraphId = p.ParagraphId, ChapterId = p.ChapterId, ParagraphOrder = p.ParagraphOrder, Raw = p.Japanese }),
                "fr" => q.Select(p => new ParagraphText { ParagraphId = p.ParagraphId, ChapterId = p.ChapterId, ParagraphOrder = p.ParagraphOrder, Raw = p.French }),
                _ => q.Select(p => new ParagraphText { ParagraphId = p.ParagraphId, ChapterId = p.ChapterId, ParagraphOrder = p.ParagraphOrder, Raw = p.English })
            };
        }

        /// <summary>Audio đã có của truyện theo ngôn ngữ: ParagraphId -> bản ghi</summary>
        public static async Task<Dictionary<int, TblParagraphAudio>> LoadExistingAsync(
            TruyenSongNguContext context, int storyId, string lang, IReadOnlyCollection<int>? chapterIds, bool tracking)
        {
            var paragraphs = context.TblParagraphs.Where(p => p.Chap.StoryId == storyId);
            if (chapterIds != null && chapterIds.Count > 0) paragraphs = paragraphs.Where(p => chapterIds.Contains(p.ChapterId));

            var audios = tracking ? context.TblParagraphAudios : context.TblParagraphAudios.AsNoTracking();
            var list = await (from a in audios
                              join p in paragraphs on a.ParagraphId equals p.ParagraphId
                              where a.Lang == lang
                              select a).ToListAsync();
            return list.GroupBy(a => a.ParagraphId).ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>Thống kê audio theo chương (dùng cho bảng chương và phần ước tính chi phí)</summary>
        public static async Task<List<ChapterAudioStatus>> ComputeStatusAsync(
            TruyenSongNguContext context, int storyId, string lang, bool overwrite, IReadOnlyCollection<int>? chapterIds = null)
        {
            var chaptersQuery = context.TblChapters.AsNoTracking().Where(c => c.StoryId == storyId);
            if (chapterIds != null && chapterIds.Count > 0) chaptersQuery = chaptersQuery.Where(c => chapterIds.Contains(c.ChapterId));
            var chapters = await chaptersQuery
                .OrderBy(c => c.ChapterNumber)
                .Select(c => new { c.ChapterId, c.ChapterNumber, c.Title })
                .ToListAsync();

            var texts = await QueryTexts(context, storyId, lang, chapterIds).ToListAsync();
            var existing = await LoadExistingAsync(context, storyId, lang, chapterIds, tracking: false);

            var byChapter = chapters.ToDictionary(c => c.ChapterId, c => new ChapterAudioStatus
            {
                ChapterId = c.ChapterId,
                ChapterNumber = c.ChapterNumber,
                Title = c.Title ?? ""
            });

            var voicesByChapter = new Dictionary<int, HashSet<string>>();
            foreach (var t in texts)
            {
                if (!byChapter.TryGetValue(t.ChapterId, out var st)) continue;
                string text = CleanText(t.Raw);
                if (text.Length == 0) continue;

                st.Total++;
                string hash = Hash(text);
                existing.TryGetValue(t.ParagraphId, out var audio);
                if (audio != null)
                {
                    if (audio.TextHash == hash) st.Done++; else st.Outdated++;
                    if (!voicesByChapter.TryGetValue(t.ChapterId, out var set)) voicesByChapter[t.ChapterId] = set = new HashSet<string>();
                    set.Add(audio.ModelId + "|" + audio.VoiceId);
                }
                if (NeedsAudio(audio, hash, overwrite))
                {
                    st.ToGenerate++;
                    st.CharsToGenerate += text.Length;
                }
            }

            foreach (var (chapterId, set) in voicesByChapter)
                byChapter[chapterId].Voices = set.ToList();

            return byChapter.Values.OrderBy(c => c.ChapterNumber).ToList();
        }
    }

    // =========================================================================
    // JOB HANGFIRE
    // =========================================================================
    public interface IParagraphAudioService
    {
        /// <summary>
        /// Tạo audio ElevenLabs cho các câu của truyện (theo chương đã chọn) bằng 1 ngôn ngữ.
        /// Mỗi câu xong là lưu ngay (dừng giữa chừng vẫn giữ phần đã tạo, chạy lại sẽ bỏ qua câu đã có).
        /// Attribute đặt ở interface vì job được tạo bằng Enqueue&lt;IParagraphAudioService&gt;.
        /// </summary>
        [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        Task RunAudioJob(AudioJobRequest request, IJobCancellationToken cancellationToken);
    }

    public class ParagraphAudioService : IParagraphAudioService
    {
        private const int MaxAttemptsPerCall = 5;
        private const int MaxConsecutiveFailures = 5;
        private const int DelayBetweenCallsMs = 300;

        private readonly TruyenSongNguContext _context;
        private readonly IElevenLabsService _elevenLabs;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ParagraphAudioService> _logger;

        public ParagraphAudioService(
            TruyenSongNguContext context,
            IElevenLabsService elevenLabs,
            IWebHostEnvironment env,
            IMemoryCache cache,
            ILogger<ParagraphAudioService> logger)
        {
            _context = context;
            _elevenLabs = elevenLabs;
            _env = env;
            _cache = cache;
            _logger = logger;
        }

        public static string CacheKey(string progressKey) => "audio-job:" + progressKey;

        public async Task RunAudioJob(AudioJobRequest request, IJobCancellationToken cancellationToken)
        {
            var progress = new AudioJobProgress { StoryId = request.StoryId, Lang = request.Lang };
            void Publish()
            {
                progress.UpdatedAt = DateTime.Now;
                if (!string.IsNullOrEmpty(request.ProgressKey))
                    _cache.Set(CacheKey(request.ProgressKey), progress, TimeSpan.FromDays(1));
            }
            Publish();

            CancellationToken ct = cancellationToken?.ShutdownToken ?? CancellationToken.None;

            try
            {
                var model = ElevenLabsService.FindModel(request.ModelId)
                    ?? throw new InvalidOperationException($"Model không hợp lệ: {request.ModelId}");
                string lang = ParagraphAudioHelper.NormalizeLang(request.Lang)
                    ?? throw new InvalidOperationException($"Ngôn ngữ không hợp lệ: {request.Lang}");
                if (!model.Languages.Contains(lang))
                    throw new InvalidOperationException($"{model.Name} không đọc được ngôn ngữ \"{lang}\". Hãy chọn model khác.");
                if (string.IsNullOrWhiteSpace(request.VoiceId))
                    throw new InvalidOperationException("Chưa chọn giọng đọc.");

                var chapterIds = request.ChapterIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
                var chapters = await _context.TblChapters.AsNoTracking()
                    .Where(c => c.StoryId == request.StoryId && (chapterIds.Count == 0 || chapterIds.Contains(c.ChapterId)))
                    .OrderBy(c => c.ChapterNumber)
                    .Select(c => new { c.ChapterId, c.ChapterNumber, c.Title })
                    .ToListAsync(ct);

                var texts = await ParagraphAudioHelper.QueryTexts(_context, request.StoryId, lang, chapterIds).ToListAsync(ct);
                var existing = await ParagraphAudioHelper.LoadExistingAsync(_context, request.StoryId, lang, chapterIds, tracking: true);

                // Danh sách việc cần làm, theo thứ tự chương -> câu
                var chapterOrder = chapters.Select((c, i) => (c.ChapterId, i)).ToDictionary(x => x.ChapterId, x => x.i);
                var work = texts
                    .Where(t => chapterOrder.ContainsKey(t.ChapterId))
                    .Select(t => (Item: t, Text: ParagraphAudioHelper.CleanText(t.Raw)))
                    .Where(x => x.Text.Length > 0)
                    .Select(x => (x.Item, x.Text, Hash: ParagraphAudioHelper.Hash(x.Text)))
                    .Where(x => ParagraphAudioHelper.NeedsAudio(existing.GetValueOrDefault(x.Item.ParagraphId), x.Hash, request.Overwrite))
                    .OrderBy(x => chapterOrder[x.Item.ChapterId]).ThenBy(x => x.Item.ParagraphOrder)
                    .ToList();

                progress.Total = work.Count;
                Publish();
                _logger.LogInformation("[Audio] Truyện {Story} – {Lang} – {Model}/{Voice}: {Count} câu cần tạo.",
                    request.StoryId, lang, model.Id, request.VoiceName, work.Count);

                int consecutiveFailures = 0;
                foreach (var (item, text, hash) in work)
                {
                    cancellationToken?.ThrowIfCancellationRequested();

                    var chapter = chapters.First(c => c.ChapterId == item.ChapterId);
                    progress.CurrentChapter = $"Chương {chapter.ChapterNumber}" + (string.IsNullOrWhiteSpace(chapter.Title) ? "" : $": {chapter.Title}");

                    try
                    {
                        byte[] audio = await SynthesizeAsync(text, request.VoiceId, model, lang, ct);

                        string webPath = ParagraphAudioHelper.WebPath(request.StoryId, item.ChapterId, item.ParagraphId, lang);
                        string physical = ParagraphAudioHelper.PhysicalPath(_env.WebRootPath, webPath)!;
                        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
                        string temp = physical + ".tmp";
                        await File.WriteAllBytesAsync(temp, audio, ct);
                        File.Move(temp, physical, overwrite: true);

                        if (existing.TryGetValue(item.ParagraphId, out var row))
                        {
                            row.ModelId = model.Id;
                            row.VoiceId = request.VoiceId;
                            row.FilePath = webPath;
                            row.TextHash = hash;
                            row.CharCount = text.Length;
                            row.CreatedAt = DateTime.Now;
                        }
                        else
                        {
                            row = new TblParagraphAudio
                            {
                                ParagraphId = item.ParagraphId,
                                Lang = lang,
                                ModelId = model.Id,
                                VoiceId = request.VoiceId,
                                FilePath = webPath,
                                TextHash = hash,
                                CharCount = text.Length,
                                CreatedAt = DateTime.Now
                            };
                            _context.TblParagraphAudios.Add(row);
                            existing[item.ParagraphId] = row;
                        }
                        await _context.SaveChangesAsync(ct);

                        progress.Done++;
                        progress.Chars += text.Length;
                        consecutiveFailures = 0;
                    }
                    catch (ElevenLabsException ex) when (!ex.IsFatal)
                    {
                        // Lỗi riêng của câu này (bị từ chối nội dung...) -> bỏ qua câu, chạy tiếp
                        progress.Failed++;
                        progress.LastError = $"Câu #{item.ParagraphOrder} ({progress.CurrentChapter}): {ex.Message}";
                        _logger.LogWarning("[Audio] Bỏ qua câu {Id}: {Error}", item.ParagraphId, ex.Message);
                        if (++consecutiveFailures >= MaxConsecutiveFailures)
                            throw new InvalidOperationException($"Dừng vì {MaxConsecutiveFailures} câu liên tiếp bị lỗi. Lỗi cuối: {ex.Message}");
                    }

                    Publish();
                    await Task.Delay(DelayBetweenCallsMs, ct);
                }

                progress.Finished = true;
                progress.CurrentChapter = null;
                Publish();
            }
            catch (OperationCanceledException)
            {
                progress.Finished = true;
                progress.Cancelled = true;
                Publish();
                throw;
            }
            catch (Exception ex)
            {
                progress.Finished = true;
                progress.LastError = ex.Message;
                Publish();
                _logger.LogError(ex, "[Audio] Job lỗi: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>Gọi TTS (cắt câu dài, thử lại khi bị giới hạn tốc độ / lỗi mạng)</summary>
        private async Task<byte[]> SynthesizeAsync(string text, string voiceId, ElevenLabsModel model, string lang, CancellationToken ct)
        {
            var chunks = ParagraphAudioHelper.SplitForTts(text, model.MaxChars);
            using var output = new MemoryStream();

            foreach (var chunk in chunks)
            {
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        var bytes = await _elevenLabs.TextToSpeechAsync(chunk, voiceId, model.Id, lang, ct);
                        // Ghép nhiều đoạn MP3 nối tiếp nhau (trình duyệt phát liền mạch)
                        await output.WriteAsync(bytes, ct);
                        break;
                    }
                    catch (ElevenLabsException ex) when (!ex.IsFatal && attempt < MaxAttemptsPerCall &&
                                                         (ex.IsRateLimit || ex.Status == null || (int)ex.Status >= 500))
                    {
                        int wait = ex.IsRateLimit ? 8 * attempt : 3 * attempt;
                        _logger.LogInformation("[Audio] {Error} – chờ {Wait}s rồi thử lại ({Attempt}/{Max}).", ex.Message, wait, attempt, MaxAttemptsPerCall);
                        await Task.Delay(TimeSpan.FromSeconds(wait), ct);
                    }
                    catch (HttpRequestException ex) when (attempt < MaxAttemptsPerCall)
                    {
                        _logger.LogInformation("[Audio] Lỗi mạng: {Error} – thử lại ({Attempt}/{Max}).", ex.Message, attempt, MaxAttemptsPerCall);
                        await Task.Delay(TimeSpan.FromSeconds(3 * attempt), ct);
                    }
                    catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < MaxAttemptsPerCall)
                    {
                        // Hết thời gian chờ HTTP (không phải người dùng bấm dừng)
                        await Task.Delay(TimeSpan.FromSeconds(3 * attempt), ct);
                    }
                    catch (HttpRequestException ex)
                    {
                        throw new ElevenLabsException("Không kết nối được ElevenLabs: " + ex.Message);
                    }
                    catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                    {
                        throw new ElevenLabsException("ElevenLabs phản hồi quá lâu (hết thời gian chờ).");
                    }
                }
            }
            return output.ToArray();
        }
    }
}

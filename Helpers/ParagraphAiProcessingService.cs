using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using static WebTruyenTranh.Services.ParagraphImportParser;

namespace WebTruyenTranh.Services
{
    public interface IParagraphAiProcessingService
    {
        /// <summary>
        /// Job import có dùng AI: dịch các ngôn ngữ được chọn + căn chỉnh các bản dịch lệch số câu,
        /// rồi ghi TẤT CẢ vào CSDL trong một lần (lỗi giữa chừng thì không ghi gì).
        /// Attribute phải đặt ở INTERFACE: job được tạo bằng Enqueue&lt;IParagraphAiProcessingService&gt;
        /// nên Hangfire đọc bộ lọc trên method của interface (đặt ở class sẽ bị bỏ qua).
        /// </summary>
        [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        Task RunImportJob(string payloadPath);
    }

    public class ParagraphAiProcessingService : IParagraphAiProcessingService
    {
        // Giới hạn thử lại – tránh job chạy mãi khi API key sai / hết quota / AI trả sai định dạng
        private const int MaxAiCallAttempts = 8;      // số lần gọi API cho 1 yêu cầu (tính cả lần chờ rate limit)
        private const int MaxBatchRounds = 4;         // số vòng gửi lại 1 batch khi AI trả thiếu / sai JSON
        private const int MaxRateLimitWaitSeconds = 90;

        private readonly TruyenSongNguContext _context;
        private readonly IAiCompletionService _ai;
        private readonly ILogger<ParagraphAiProcessingService> _logger;

        /// <summary>Model AI của job đang chạy (service tạo mới cho mỗi job)</summary>
        private string _modelId = "";

        public ParagraphAiProcessingService(
            TruyenSongNguContext context,
            IAiCompletionService ai,
            ILogger<ParagraphAiProcessingService> logger)
        {
            _context = context;
            _ai = ai;
            _logger = logger;
        }

        // Không để Hangfire tự chạy lại: job đã gọi AI rất nhiều lần, chạy lại tự động chỉ tốn quota
        // và trước đây còn gây "thành công giả" (file tạm đã bị xóa). Lỗi -> trạng thái Failed + thông báo rõ.
        [AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task RunImportJob(string payloadPath)
        {
            try
            {
                var payload = LoadPayload(payloadPath)
                    ?? throw new InvalidOperationException("Không tìm thấy dữ liệu import (file tạm đã bị xóa). Hãy import lại.");

                int count = payload.English.Count;
                if (count == 0) throw new InvalidOperationException("File tiếng Anh nguồn không có câu nào.");

                var model = _ai.Find(payload.AiModel) ?? _ai.DefaultModel;
                _modelId = model.Id;

                _logger.LogInformation("[Import AI] Chapter {Chapter}: {Count} câu, model {Model}, dịch AI: [{Ai}], căn chỉnh: [{Align}]",
                    payload.ChapterId, count, model.Name, string.Join(",", payload.AiLanguages), string.Join(",", payload.ToAlign.Keys));

                var translations = new Dictionary<string, List<string>>(payload.Provided);

                // 1. Căn chỉnh các bản dịch lệch số câu
                foreach (var (lang, rawLines) in payload.ToAlign)
                {
                    translations[lang] = await AlignLanguageAsync(lang, payload.English, rawLines);
                }

                // 2. AI dịch các ngôn ngữ được chọn
                if (payload.AiLanguages.Count > 0)
                {
                    var translated = await TranslateAsync(payload.English, payload.AiLanguages);
                    foreach (var (lang, lines) in translated) translations[lang] = lines;
                }

                // 3. Ghi một lần, trong transaction
                int saved = await SaveAsync(_context, payload.ChapterId, payload.English, translations);
                _logger.LogInformation("[Import AI] Đã lưu {Saved} câu vào chương {Chapter}.", saved, payload.ChapterId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Import AI] Job lỗi: {Message}", ex.Message);
                throw;   // để Hangfire ghi trạng thái Failed (CheckJobStatus hiện thông báo này cho admin)
            }
            finally
            {
                // Không còn tự chạy lại nên xóa file tạm an toàn
                try { if (File.Exists(payloadPath)) File.Delete(payloadPath); } catch { }
            }
        }

        // ------------------------------------------------------------------
        // DỊCH
        // ------------------------------------------------------------------
        private async Task<Dictionary<string, List<string>>> TranslateAsync(List<Block> english, List<string> languages)
        {
            var result = languages.ToDictionary(l => l, _ => Enumerable.Repeat("", english.Count).ToList());
            var (chunkSize, delayMs) = CalculateBatchConfig(languages.Count);
            string langList = string.Join(", ", languages.Select(DisplayName));

            for (int start = 0; start < english.Count; start += chunkSize)
            {
                // Chỉ số (trong toàn chương) của các câu còn thiếu bản dịch trong batch này
                var pending = Enumerable.Range(start, Math.Min(chunkSize, english.Count - start)).ToList();

                for (int round = 1; round <= MaxBatchRounds && pending.Count > 0; round++)
                {
                    // id gửi cho AI = vị trí trong batch (1..n) – ghép lại theo id chứ không theo thứ tự trả về
                    var idMap = pending.Select((globalIndex, k) => (Id: k + 1, Index: globalIndex)).ToList();
                    var input = idMap.Select(x => new { id = x.Id, text = english[x.Index].Text });

                    var rules = new StringBuilder();
                    foreach (var lang in languages)
                        rules.AppendLine($"- Translate \"text\" into {DisplayName(lang)}, put it in key \"{lang}\".");

                    string prompt = $@"You are a JSON-only translation API. Translate these English story sentences into: {langList}.

RULES:
{rules}- Keep the same ""id"" for every item. Translate EVERY item, do not merge or skip.
- Natural, literary style suitable for a novel. Keep names unchanged.

INPUT:
{JsonSerializer.Serialize(input)}

OUTPUT: a JSON array only, no markdown, no explanation. Example:
[{{""id"": 1, {string.Join(", ", languages.Select(l => $"\"{l}\": \"...\""))}}}]";

                    string label = $"dịch câu {pending.First() + 1}-{pending.Last() + 1}/{english.Count}";
                    var items = await CallAiForJsonArrayAsync(prompt, label);

                    var done = new HashSet<int>();
                    foreach (var item in items)
                    {
                        int? id = ReadId(item);
                        if (id == null) continue;
                        var target = idMap.FirstOrDefault(x => x.Id == id.Value);
                        if (target.Id == 0) continue;

                        bool complete = true;
                        foreach (var lang in languages)
                        {
                            string? text = ExtractLang(item, lang);
                            if (string.IsNullOrWhiteSpace(text)) { complete = false; continue; }
                            result[lang][target.Index] = text;
                        }
                        if (complete) done.Add(target.Index);
                    }

                    pending = pending.Where(i => !done.Contains(i)).ToList();
                    if (pending.Count > 0)
                    {
                        _logger.LogWarning("[Import AI] {Label}: AI trả thiếu {Missing} câu (vòng {Round}/{Max}), gửi lại phần thiếu.",
                            label, pending.Count, round, MaxBatchRounds);
                        await Task.Delay(2500);
                    }
                }

                if (pending.Count > 0)
                    throw new InvalidOperationException(
                        $"AI không dịch được câu {string.Join(", ", pending.Take(5).Select(i => i + 1))}" +
                        $"{(pending.Count > 5 ? "..." : "")} sau {MaxBatchRounds} lần thử. Chưa có gì được lưu, hãy thử lại sau.");

                _logger.LogInformation("[Import AI] Đã dịch tới câu {Done}/{Total}.", Math.Min(start + chunkSize, english.Count), english.Count);
                if (start + chunkSize < english.Count) await Task.Delay(delayMs);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // CĂN CHỈNH BẢN DỊCH LỆCH SỐ CÂU
        // ------------------------------------------------------------------
        private async Task<List<string>> AlignLanguageAsync(string lang, List<Block> english, List<string> rawLines)
        {
            int target = english.Count;
            if (rawLines.Count == target) return rawLines;

            var aligned = new List<string>(target);
            const int batchSize = 7;   // ~3.500 token/lần, tránh trần TPM của Groq
            int totalBatches = (int)Math.Ceiling((double)target / batchSize);

            for (int b = 0; b < totalBatches; b++)
            {
                var enBatch = english.Skip(b * batchSize).Take(batchSize).ToList();
                int n = enBatch.Count;

                // Lấy phần bản dịch tương ứng theo TỈ LỆ, các batch KHÔNG chồng lên nhau
                // (trước đây Math.Max làm lấy dư sang batch sau -> 1 câu bị dùng 2 lần)
                int rawStart = (int)Math.Round((double)b * rawLines.Count / totalBatches);
                int rawEnd = (int)Math.Round((double)(b + 1) * rawLines.Count / totalBatches);
                if (b == totalBatches - 1) rawEnd = rawLines.Count;
                var rawBatch = rawLines.Skip(rawStart).Take(Math.Max(0, rawEnd - rawStart)).ToList();

                var source = enBatch.Select((x, i) => new { id = i + 1, en = x.Text });
                var raw = rawBatch.Select((x, i) => new { raw_id = i + 1, text = x });

                string prompt = $@"You are a JSON-only API. Align {DisplayName(lang)} translation lines with the English source.

ENGLISH SOURCE ({n} items):
{JsonSerializer.Serialize(source)}

RAW {DisplayName(lang).ToUpperInvariant()} LINES (may be split or merged differently):
{JsonSerializer.Serialize(raw)}

TASK: Re-split or merge the {DisplayName(lang)} text so it matches the {n} English items EXACTLY 1-to-1.
Use only the given {DisplayName(lang)} text. If an English item has no counterpart, return an empty string for it.

OUTPUT: ONLY a JSON array of {n} objects with keys ""id"" (1 to {n}) and ""text"". No markdown, no explanation.
[{{""id"": 1, ""text"": ""...""}}]";

                string label = $"căn chỉnh {DisplayName(lang)} phần {b + 1}/{totalBatches}";
                Dictionary<int, string>? byId = null;

                for (int round = 1; round <= MaxBatchRounds; round++)
                {
                    var items = await CallAiForJsonArrayAsync(prompt, label);
                    var map = new Dictionary<int, string>();
                    foreach (var item in items)
                    {
                        int? id = ReadId(item);
                        if (id is >= 1 && id <= n && item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                            map[id.Value] = t.GetString() ?? "";
                    }

                    if (map.Count == n) { byId = map; break; }

                    _logger.LogWarning("[Import AI] {Label}: AI trả {Got}/{Need} câu (vòng {Round}/{Max}).", label, map.Count, n, round, MaxBatchRounds);
                    await Task.Delay(3000);
                }

                if (byId == null)
                    throw new InvalidOperationException(
                        $"AI không căn chỉnh được bản {DisplayName(lang)} (phần {b + 1}/{totalBatches}) sau {MaxBatchRounds} lần thử. " +
                        "Chưa có gì được lưu. Hãy kiểm tra file bản dịch có đúng là bản dịch của file tiếng Anh không.");

                for (int i = 1; i <= n; i++) aligned.Add(byId[i]);
                if (b < totalBatches - 1) await Task.Delay(3500);   // cho TPM hồi lại
            }
            return aligned;
        }

        // ------------------------------------------------------------------
        // GỌI AI (có giới hạn) + ĐỌC JSON
        // ------------------------------------------------------------------
        private async Task<List<JsonElement>> CallAiForJsonArrayAsync(string prompt, string label)
        {
            string lastError = "";

            for (int attempt = 1; attempt <= MaxAiCallAttempts; attempt++)
            {
                string response;
                try
                {
                    response = await _ai.CompleteAsync(_modelId, prompt);
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    if (IsRateLimit(ex.Message))
                    {
                        double wait = 15;
                        var m = Regex.Match(ex.Message, @"try again in (?:(\d+)m)?(\d+(?:\.\d+)?)s", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            double min = m.Groups[1].Success ? double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
                            wait = min * 60 + double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) + 2;
                        }
                        // Chờ quá lâu (hết quota theo ngày) -> dừng luôn, báo lỗi
                        if (wait > MaxRateLimitWaitSeconds)
                            throw new InvalidOperationException($"AI đã hết hạn mức sử dụng ({label}). Hãy thử lại sau. Chi tiết: {ex.Message}");

                        _logger.LogInformation("[Import AI] {Label}: chạm giới hạn tốc độ, chờ {Wait:F0}s (lần {Attempt}/{Max}).", label, wait, attempt, MaxAiCallAttempts);
                        await Task.Delay(TimeSpan.FromSeconds(wait));
                    }
                    else
                    {
                        // Lỗi không phải rate limit (API key sai, model không tồn tại...) thường không tự hết
                        _logger.LogWarning("[Import AI] {Label}: lỗi gọi AI (lần {Attempt}/{Max}): {Error}", label, attempt, MaxAiCallAttempts, ex.Message);
                        if (IsFatal(ex.Message))
                            throw new InvalidOperationException($"Không gọi được AI ({label}): {ex.Message}");
                        await Task.Delay(4000);
                    }
                    continue;
                }

                if (string.IsNullOrWhiteSpace(response))
                {
                    lastError = "AI trả về rỗng";
                    await Task.Delay(3000);
                    continue;
                }

                var items = ParseJsonArray(response);
                if (items != null) return items;

                lastError = "AI trả về không đúng định dạng JSON";
                _logger.LogWarning("[Import AI] {Label}: JSON lỗi (lần {Attempt}/{Max}).", label, attempt, MaxAiCallAttempts);
                await Task.Delay(3000);
            }

            throw new InvalidOperationException($"AI không phản hồi hợp lệ sau {MaxAiCallAttempts} lần thử ({label}). Lỗi cuối: {lastError}");
        }

        private static bool IsRateLimit(string msg) =>
            msg.Contains("Rate limit", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("429", StringComparison.Ordinal) ||
            msg.Contains("TPM", StringComparison.Ordinal) ||
            msg.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase);

        private static bool IsFatal(string msg) =>
            msg.Contains("401", StringComparison.Ordinal) ||
            msg.Contains("403", StringComparison.Ordinal) ||
            msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("Invalid API Key", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("model_not_found", StringComparison.OrdinalIgnoreCase);

        /// <summary>Bóc mảng JSON từ câu trả lời của AI (bỏ thẻ think, code fence, sửa mảng bị cắt cụt). null = không đọc được</summary>
        private static List<JsonElement>? ParseJsonArray(string response)
        {
            string json = Regex.Replace(response, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();
            json = Regex.Replace(json, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase);
            json = Regex.Replace(json, @"\s*```\s*$", "");

            int first = json.IndexOf('[');
            if (first < 0) return null;
            json = json.Substring(first).Trim();

            if (!json.EndsWith("]"))
            {
                int lastBrace = json.LastIndexOf('}');
                if (lastBrace < 0) return null;
                json = json.Substring(0, lastBrace + 1) + "]";
            }
            else
            {
                // Cắt phần thừa sau dấu ] cuối cùng của mảng
                json = json.Substring(0, json.LastIndexOf(']') + 1);
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
                return doc.RootElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).Select(e => e.Clone()).ToList();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static int? ReadId(JsonElement item)
        {
            if (!item.TryGetProperty("id", out var idProp)) return null;
            if (idProp.ValueKind == JsonValueKind.Number && idProp.TryGetInt32(out int n)) return n;
            if (idProp.ValueKind == JsonValueKind.String && int.TryParse(idProp.GetString(), out int s)) return s;
            return null;
        }

        /// <summary>Lấy bản dịch 1 ngôn ngữ trong object AI trả về (chấp nhận vài biến thể tên key)</summary>
        private static string? ExtractLang(JsonElement item, string lang)
        {
            string[] keys = lang switch
            {
                Vietnamese => new[] { "vietnamese", "vi", "vie", "tieng_viet" },
                Chinese => new[] { "chinese", "zh", "cn", "zho", "tieng_trung" },
                Japanese => new[] { "japanese", "ja", "jp", "jpn", "tieng_nhat" },
                French => new[] { "french", "fr", "fra", "tieng_phap" },
                _ => new[] { lang }
            };

            foreach (var prop in item.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String) continue;
                if (keys.Contains(prop.Name.Trim().ToLowerInvariant()))
                {
                    var s = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                }
            }
            return null;
        }

        private static string DisplayName(string lang) => lang switch
        {
            Vietnamese => "Vietnamese",
            Chinese => "Chinese (Simplified)",
            Japanese => "Japanese",
            French => "French",
            _ => lang
        };

        private static (int chunkSize, int delayMs) CalculateBatchConfig(int langCount) => langCount switch
        {
            1 => (12, 2500),
            2 => (7, 3500),
            3 => (4, 4500),
            _ => (3, 6000)
        };
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WebTruyenTranh.Services
{
    /// <summary>
    /// Gọi API ElevenLabs: danh sách giọng, hạn mức ký tự, chuyển văn bản thành giọng nói (mp3).
    /// API key đặt trong User Secrets / biến môi trường: "ElevenLabs:ApiKey" (KHÔNG để trong appsettings.json).
    /// </summary>
    public interface IElevenLabsService
    {
        bool IsConfigured { get; }
        Task<List<ElevenLabsVoice>> GetVoicesAsync(CancellationToken ct = default);
        Task<ElevenLabsQuota?> GetQuotaAsync(CancellationToken ct = default);
        Task<byte[]> TextToSpeechAsync(string text, string voiceId, string modelId, string langCode, CancellationToken ct = default);
    }

    public class ElevenLabsVoice
    {
        public string VoiceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string PreviewUrl { get; set; } = "";
    }

    public class ElevenLabsQuota
    {
        public long Used { get; set; }
        public long Limit { get; set; }
        public long Remaining => Math.Max(0, Limit - Used);
        public DateTime? ResetAtUtc { get; set; }
    }

    /// <summary>Thông tin một model giọng nói ElevenLabs dùng trong trang Admin</summary>
    public class ElevenLabsModel
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Note { get; init; } = "";
        /// <summary>Ngôn ngữ (mã 2 chữ) model đọc được trong số ngôn ngữ của web</summary>
        public string[] Languages { get; init; } = Array.Empty<string>();
        /// <summary>Số credit cho 1 ký tự (ước tính, gói tự phục vụ)</summary>
        public double CreditPerChar { get; init; } = 1;
        /// <summary>Số ký tự tối đa mỗi lần gọi (cắt nhỏ nếu dài hơn)</summary>
        public int MaxChars { get; init; } = 5000;
        /// <summary>Model có nhận tham số language_code không</summary>
        public bool SupportsLanguageCode { get; init; }
    }

    public class ElevenLabsException : Exception
    {
        public HttpStatusCode? Status { get; }
        /// <summary>Lỗi không tự hết khi thử lại (sai key, hết hạn mức, giọng không tồn tại...)</summary>
        public bool IsFatal { get; }
        public bool IsRateLimit => Status == HttpStatusCode.TooManyRequests;

        public ElevenLabsException(string message, HttpStatusCode? status = null, bool isFatal = false) : base(message)
        {
            Status = status;
            IsFatal = isFatal;
        }
    }

    public class ElevenLabsService : IElevenLabsService
    {
        private const string BaseUrl = "https://api.elevenlabs.io";

        /// <summary>
        /// Các model cho trang Admin. Lưu ý: Multilingual v2 KHÔNG đọc được tiếng Việt.
        /// </summary>
        public static readonly IReadOnlyList<ElevenLabsModel> Models = new[]
        {
            new ElevenLabsModel
            {
                Id = "eleven_v3", Name = "Eleven v3",
                Note = "Giàu cảm xúc nhất, hợp truyện drama",
                Languages = new[] { "en", "vi", "zh", "ja", "fr" },
                CreditPerChar = 1, MaxChars = 4500, SupportsLanguageCode = true
            },
            new ElevenLabsModel
            {
                Id = "eleven_flash_v2_5", Name = "Flash v2.5",
                Note = "Rẻ (~0.5 credit/ký tự), nhanh",
                Languages = new[] { "en", "vi", "zh", "ja", "fr" },
                CreditPerChar = 0.5, MaxChars = 9000, SupportsLanguageCode = true
            },
            new ElevenLabsModel
            {
                Id = "eleven_multilingual_v2", Name = "Multilingual v2",
                Note = "Ổn định, tự nhiên – KHÔNG có tiếng Việt",
                Languages = new[] { "en", "zh", "ja", "fr" },
                CreditPerChar = 1, MaxChars = 9000, SupportsLanguageCode = false
            }
        };

        public static ElevenLabsModel? FindModel(string? id) => Models.FirstOrDefault(m => m.Id == id);

        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly ILogger<ElevenLabsService> _logger;

        public ElevenLabsService(HttpClient http, IConfiguration configuration, ILogger<ElevenLabsService> logger)
        {
            _http = http;
            _http.Timeout = TimeSpan.FromSeconds(120);
            _apiKey = configuration["ElevenLabs:ApiKey"] ?? "";
            _logger = logger;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        private HttpRequestMessage NewRequest(HttpMethod method, string path)
        {
            if (!IsConfigured)
                throw new ElevenLabsException("Chưa cấu hình ElevenLabs:ApiKey (đặt trong User Secrets).", isFatal: true);
            var req = new HttpRequestMessage(method, BaseUrl + path);
            req.Headers.Add("xi-api-key", _apiKey);
            return req;
        }

        public async Task<List<ElevenLabsVoice>> GetVoicesAsync(CancellationToken ct = default)
        {
            var result = new List<ElevenLabsVoice>();
            string? token = null;

            // Tối đa 5 trang x 100 giọng
            for (int page = 0; page < 5; page++)
            {
                string path = "/v2/voices?page_size=100" + (token != null ? "&next_page_token=" + Uri.EscapeDataString(token) : "");
                using var req = NewRequest(HttpMethod.Get, path);
                using var res = await _http.SendAsync(req, ct);
                string body = await res.Content.ReadAsStringAsync(ct);
                if (!res.IsSuccessStatusCode) throw BuildError(res.StatusCode, body);

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("voices", out var voices) && voices.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in voices.EnumerateArray())
                    {
                        result.Add(new ElevenLabsVoice
                        {
                            VoiceId = Str(v, "voice_id"),
                            Name = Str(v, "name"),
                            Category = Str(v, "category"),
                            Description = Str(v, "description"),
                            PreviewUrl = Str(v, "preview_url")
                        });
                    }
                }

                bool hasMore = root.TryGetProperty("has_more", out var hm) && hm.ValueKind == JsonValueKind.True;
                token = root.TryGetProperty("next_page_token", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                if (!hasMore || string.IsNullOrEmpty(token)) break;
            }

            return result.Where(v => !string.IsNullOrEmpty(v.VoiceId)).OrderBy(v => v.Name).ToList();
        }

        public async Task<ElevenLabsQuota?> GetQuotaAsync(CancellationToken ct = default)
        {
            using var req = NewRequest(HttpMethod.Get, "/v1/user/subscription");
            using var res = await _http.SendAsync(req, ct);
            string body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw BuildError(res.StatusCode, body);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            long Num(string name) => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : 0;

            DateTime? reset = null;
            if (root.TryGetProperty("next_character_count_reset_unix", out var r) && r.ValueKind == JsonValueKind.Number)
                reset = DateTimeOffset.FromUnixTimeSeconds(r.GetInt64()).UtcDateTime;

            return new ElevenLabsQuota { Used = Num("character_count"), Limit = Num("character_limit"), ResetAtUtc = reset };
        }

        public async Task<byte[]> TextToSpeechAsync(string text, string voiceId, string modelId, string langCode, CancellationToken ct = default)
        {
            var model = FindModel(modelId) ?? throw new ElevenLabsException($"Model không hợp lệ: {modelId}", isFatal: true);

            var payload = new Dictionary<string, object?>
            {
                ["text"] = text,
                ["model_id"] = model.Id
            };
            // Multilingual v2 không nhận language_code (gửi lên có thể bị lỗi)
            if (model.SupportsLanguageCode && !string.IsNullOrEmpty(langCode)) payload["language_code"] = langCode;

            using var req = NewRequest(HttpMethod.Post, $"/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format=mp3_44100_128");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                string body = await res.Content.ReadAsStringAsync(ct);
                throw BuildError(res.StatusCode, body);
            }

            var bytes = await res.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length < 100) throw new ElevenLabsException("ElevenLabs trả về file audio rỗng.");
            return bytes;
        }

        /// <summary>Đổi lỗi API thành thông báo dễ hiểu; đánh dấu lỗi nào không nên thử lại</summary>
        private ElevenLabsException BuildError(HttpStatusCode status, string body)
        {
            string detailStatus = "", detailMessage = body;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var d))
                {
                    if (d.ValueKind == JsonValueKind.Object)
                    {
                        detailStatus = Str(d, "status");
                        detailMessage = Str(d, "message");
                    }
                    else if (d.ValueKind == JsonValueKind.String)
                    {
                        detailMessage = d.GetString() ?? body;
                    }
                    else
                    {
                        detailMessage = d.ToString();
                    }
                }
            }
            catch (JsonException) { }

            if (detailMessage.Length > 300) detailMessage = detailMessage.Substring(0, 300);
            _logger.LogWarning("[ElevenLabs] {Status} {DetailStatus}: {Message}", (int)status, detailStatus, detailMessage);

            if (detailStatus == "quota_exceeded")
                return new ElevenLabsException("Tài khoản ElevenLabs đã hết ký tự trong kỳ này.", status, isFatal: true);
            if (status == HttpStatusCode.Unauthorized || status == HttpStatusCode.Forbidden)
                return new ElevenLabsException($"API key ElevenLabs không hợp lệ hoặc không đủ quyền ({detailMessage}).", status, isFatal: true);
            if (status == HttpStatusCode.NotFound)
                return new ElevenLabsException($"Không tìm thấy giọng đọc / tài nguyên trên ElevenLabs ({detailMessage}).", status, isFatal: true);
            if (status == HttpStatusCode.TooManyRequests)
                return new ElevenLabsException($"ElevenLabs đang giới hạn tốc độ ({detailMessage}).", status);
            if (status == HttpStatusCode.BadRequest || status == HttpStatusCode.UnprocessableEntity)
                return new ElevenLabsException($"ElevenLabs từ chối yêu cầu: {detailMessage}", status);

            return new ElevenLabsException($"Lỗi ElevenLabs ({(int)status}): {detailMessage}", status);
        }

        private static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WebTruyenTranh.Services
{
    /// <summary>Một model AI có thể chọn ở modal Import (dịch / căn chỉnh câu)</summary>
    public class AiModelOption
    {
        /// <summary>Mã dùng trong form và payload, ví dụ "claude:claude-sonnet-5-5"</summary>
        public string Id => $"{Provider}:{Model}";
        public string Provider { get; init; } = "";   // groq | claude
        public string Model { get; init; } = "";
        public string Name { get; init; } = "";
        public string Note { get; init; } = "";
        /// <summary>Model mặc định của nhà cung cấp</summary>
        public bool IsDefault { get; init; }
    }

    /// <summary>
    /// Gọi AI theo model admin chọn: Groq (OpenAI-compatible) hoặc Claude (Anthropic Messages API).
    /// API key (User Secrets / biến môi trường, KHÔNG để trong appsettings.json):
    ///   Groq   : "Groq:ApiKey"
    ///   Claude : "Anthropic:ApiKey"
    /// Lỗi trả về dạng thông báo có mã HTTP (429, 401...) và "try again in Xs" để
    /// ParagraphAiProcessingService nhận biết giới hạn tốc độ / lỗi không nên thử lại.
    /// </summary>
    public interface IAiCompletionService
    {
        IReadOnlyList<AiModelOption> Models { get; }
        bool IsConfigured(string provider);
        AiModelOption? Find(string? id);
        AiModelOption DefaultModel { get; }
        Task<string> CompleteAsync(string modelId, string prompt, CancellationToken ct = default);
    }

    public class AiCompletionService : IAiCompletionService
    {
        public const string Groq = "groq";
        public const string Claude = "claude";

        private static readonly AiModelOption[] AllModels =
        {
            new() { Provider = Groq, Model = "openai/gpt-oss-120b", Name = "Groq · GPT-OSS 120B", Note = "Miễn phí (giới hạn tốc độ), đang dùng trước đây", IsDefault = true },
            new() { Provider = Groq, Model = "llama-3.3-70b-versatile", Name = "Groq · Llama 3.3 70B", Note = "Miễn phí, nhanh" },
            new() { Provider = Claude, Model = "claude-haiku-5-5", Name = "Claude Haiku 5.5", Note = "Rẻ nhất (~0,0015 USD/chương 1.000 từ)" },
            new() { Provider = Claude, Model = "claude-sonnet-5-5", Name = "Claude Sonnet 5.5", Note = "Văn mượt, hợp truyện (~0,03 USD/chương)", IsDefault = true },
            new() { Provider = Claude, Model = "claude-opus-5-5", Name = "Claude Opus 5.5", Note = "Chất lượng cao nhất (~0,06 USD/chương)" }
        };

        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<AiCompletionService> _logger;

        public AiCompletionService(HttpClient http, IConfiguration config, ILogger<AiCompletionService> logger)
        {
            _http = http;
            _http.Timeout = TimeSpan.FromSeconds(180);
            _config = config;
            _logger = logger;
        }

        public IReadOnlyList<AiModelOption> Models => AllModels;

        private string GroqKey => _config["Groq:ApiKey"] ?? _config["Gemini:ApiKey"] ?? "";
        private string ClaudeKey => _config["Anthropic:ApiKey"] ?? "";

        public bool IsConfigured(string provider) => provider switch
        {
            Groq => !string.IsNullOrWhiteSpace(GroqKey),
            Claude => !string.IsNullOrWhiteSpace(ClaudeKey),
            _ => false
        };

        public AiModelOption? Find(string? id) =>
            string.IsNullOrWhiteSpace(id) ? null : AllModels.FirstOrDefault(m => m.Id == id.Trim());

        /// <summary>Model mặc định: Groq GPT-OSS nếu có key Groq (giữ như cũ), không thì Claude Sonnet</summary>
        public AiModelOption DefaultModel =>
            AllModels.FirstOrDefault(m => m.IsDefault && IsConfigured(m.Provider) && m.Provider == Groq)
            ?? AllModels.FirstOrDefault(m => m.IsDefault && IsConfigured(m.Provider))
            ?? AllModels[0];

        public Task<string> CompleteAsync(string modelId, string prompt, CancellationToken ct = default)
        {
            var model = Find(modelId) ?? throw new InvalidOperationException($"Model AI không hợp lệ: {modelId}");
            if (!IsConfigured(model.Provider))
                throw new InvalidOperationException($"401 Chưa cấu hình API key cho {model.Name} ({(model.Provider == Claude ? "Anthropic:ApiKey" : "Groq:ApiKey")}).");

            return model.Provider == Claude ? CallClaudeAsync(model, prompt, ct) : CallGroqAsync(model, prompt, ct);
        }

        // ------------------------------------------------------------------
        // GROQ (OpenAI-compatible)
        // ------------------------------------------------------------------
        private async Task<string> CallGroqAsync(AiModelOption model, string prompt, CancellationToken ct)
        {
            var payload = new
            {
                model = model.Model,
                messages = new[]
                {
                    new { role = "system", content = "You are a professional literary translator. Always output valid raw JSON array only." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 4096,
                temperature = 0.2
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GroqKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct);
            string body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Groq] {Status}: {Body}", (int)res.StatusCode, Trim(body));
                // Groq tự ghi "Please try again in 7.5s" trong thông báo lỗi
                throw new HttpRequestException($"Lỗi Groq API [{(int)res.StatusCode} {res.StatusCode}]: {Trim(body)}", null, res.StatusCode);
            }

            using var doc = JsonDocument.Parse(body);
            var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
            string content = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(content) && message.TryGetProperty("reasoning_content", out var r) && r.ValueKind == JsonValueKind.String)
                content = r.GetString() ?? "";
            return content.Trim();
        }

        // ------------------------------------------------------------------
        // CLAUDE (Anthropic Messages API)
        // ------------------------------------------------------------------
        private async Task<string> CallClaudeAsync(AiModelOption model, string prompt, CancellationToken ct)
        {
            var payload = new
            {
                model = model.Model,
                max_tokens = 8192,
                // Không gửi temperature: một số model Claude mới không cho chỉnh, mặc định đã ổn cho dịch
                system = "You are a professional literary translator for a bilingual novel website. Output ONLY a valid raw JSON array, no markdown, no explanation.",
                messages = new[] { new { role = "user", content = prompt } }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            req.Headers.Add("x-api-key", ClaudeKey);
            req.Headers.Add("anthropic-version", _config["Anthropic:Version"] ?? "2023-06-01");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct);
            string body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                string type = "", message = Trim(body);
                try
                {
                    using var err = JsonDocument.Parse(body);
                    if (err.RootElement.TryGetProperty("error", out var e))
                    {
                        type = e.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                        message = e.TryGetProperty("message", out var m) ? m.GetString() ?? message : message;
                    }
                }
                catch (JsonException) { }

                _logger.LogWarning("[Claude] {Status} {Type}: {Message}", (int)res.StatusCode, type, message);

                // 429 / 529: thêm "try again in Xs" theo header retry-after để bên gọi chờ đúng thời gian
                string retry = "";
                if (res.StatusCode == HttpStatusCode.TooManyRequests || (int)res.StatusCode == 529)
                {
                    double seconds = res.Headers.RetryAfter?.Delta?.TotalSeconds
                        ?? (res.Headers.TryGetValues("retry-after", out var v) && double.TryParse(v.FirstOrDefault(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 10);
                    retry = $" Please try again in {Math.Max(1, seconds).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}s.";
                    if ((int)res.StatusCode == 529) type = "rate_limit_exceeded (overloaded)";
                }

                throw new HttpRequestException($"Lỗi Claude API [{(int)res.StatusCode} {type}]: {message}.{retry}", null, res.StatusCode);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var sb = new StringBuilder();
            if (root.TryGetProperty("content", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in blocks.EnumerateArray())
                {
                    if (b.TryGetProperty("type", out var t) && t.GetString() == "text" && b.TryGetProperty("text", out var txt))
                        sb.Append(txt.GetString());
                }
            }

            if (root.TryGetProperty("stop_reason", out var stop) && stop.GetString() == "refusal")
                throw new InvalidOperationException("Claude từ chối dịch đoạn này (stop_reason = refusal).");

            return sb.ToString().Trim();
        }

        private static string Trim(string s) => s.Length > 400 ? s.Substring(0, 400) + "…" : s;
    }
}

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebTruyenTranh.Helpers;

namespace WebTruyenTranh.Services
{
    public class GeminiTranslationService : IAiTranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiTranslationService> _logger;

        public GeminiTranslationService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GeminiTranslationService> logger)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(120);
            _apiKey = configuration["Groq:ApiKey"] ?? configuration["Gemini:ApiKey"] ?? "";
            _logger = logger;
        }

        /// <summary>
        /// Hàm dịch chuỗi đơn (Tiêu đề, tóm tắt truyện, tên chương) theo yêu cầu của IAiTranslationService
        /// </summary>
        public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string endpoint = "https://api.groq.com/openai/v1/chat/completions";

            // Prompt hướng dẫn dịch văn phong truyện tranh và chỉ trả text thuần
            var requestPayload = new
            {
                model = "openai/gpt-oss-120b", // Model dịch nhanh và chuẩn ngữ cảnh trên Groq
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = $"You are a professional comic and literature translator. Translate the given text from {fromLang} to {toLang}. Rules:\n1. Maintain natural flow, nuance, and comic/manga naming conventions.\n2. Output ONLY the translated text. Do NOT wrap in quotes, code blocks, or add explanations."
                    },
                    new
                    {
                        role = "user",
                        content = text.Trim()
                    }
                },
                max_tokens = 1024,
                temperature = 0.2
            };

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"[Groq API Error] {response.StatusCode} | {responseString}");
                throw new Exception($"Lỗi Groq API [{response.StatusCode}]: {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var messageElem = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message");

            string content = "";
            if (messageElem.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
            {
                content = contentProp.GetString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(content) && messageElem.TryGetProperty("reasoning_content", out var reasoningProp))
            {
                content = reasoningProp.GetString() ?? "";
            }

            return content.Trim().Trim('\"');
        }

        /// <summary>
        /// Hàm gửi raw prompt (dùng cho dịch batch đoạn văn trả về JSON array)
        /// </summary>
        public async Task<string> GetAiResponse(string prompt)
        {
            string endpoint = "https://api.groq.com/openai/v1/chat/completions";

            var requestPayload = new
            {
                model = "openai/gpt-oss-120b",
                messages = new[]
                {
                    new { role = "system", content = "You are a professional literary translator. Always output valid raw JSON array only." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 4096,
                temperature = 0.2
            };

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"[Groq API Error] {response.StatusCode} | {responseString}");
                throw new Exception($"Lỗi Groq API [{response.StatusCode}]: {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var messageElem = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message");

            string content = "";
            if (messageElem.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
            {
                content = contentProp.GetString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(content) && messageElem.TryGetProperty("reasoning_content", out var reasoningProp))
            {
                content = reasoningProp.GetString() ?? "";
            }

            return content.Trim();
        }
    }
}
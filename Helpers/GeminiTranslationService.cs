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
                _httpClient.Timeout = TimeSpan.FromSeconds(120); // Đặt trần timeout 2 phút
                _apiKey = configuration["Groq:ApiKey"] ?? configuration["Gemini:ApiKey"] ?? "";
                _logger = logger;
            }

        public async Task<string> GetAiResponse(string prompt)
        {
            string endpoint = "https://api.groq.com/openai/v1/chat/completions";

            var requestPayload = new
            {
                model = "groq/compound-mini",
                messages = new[]
                 {
                    new { role = "system", content = "You are a professional literary translator. Always output valid raw JSON array only." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 4096, // Tăng trần token output để không bị cụt đuôi JSON
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

            // Đọc content, nếu model trả về trong reasoning thì vẫn bắt được
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
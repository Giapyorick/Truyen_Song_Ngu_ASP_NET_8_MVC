using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
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

        public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string endpoint = "https://api.groq.com/openai/v1/chat/completions";

            var requestPayload = new
            {
                model = "openai/gpt-oss-120b",
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

        public async IAsyncEnumerable<string> StreamAiResponseAsync(
            string prompt,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string endpoint = "https://api.groq.com/openai/v1/chat/completions";

            var requestPayload = new
            {
                model = "openai/gpt-oss-120b",
                stream = true,
                messages = new[]
                {
                    new { role = "system", content = "You are a professional reading assistant and language tutor." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 2048,
                temperature = 0.3
            };

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError($"[Groq Stream Error] {response.StatusCode} | {err}");
                yield return $"[Error {response.StatusCode}]";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6).Trim();
                    if (data == "[DONE]") break;

                    string deltaContent = "";
                    try
                    {
                        using var doc = JsonDocument.Parse(data);
                        var choices = doc.RootElement.GetProperty("choices");
                        if (choices.GetArrayLength() > 0)
                        {
                            var delta = choices[0].GetProperty("delta");
                            if (delta.TryGetProperty("content", out var contentElem))
                            {
                                deltaContent = contentElem.GetString() ?? "";
                            }
                        }
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrEmpty(deltaContent))
                    {
                        yield return deltaContent;
                    }
                }
            }
        }
    }
}
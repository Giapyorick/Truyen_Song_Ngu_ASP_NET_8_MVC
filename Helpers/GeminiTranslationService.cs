using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WebTruyenTranh.Helpers;
public class GeminiTranslationService : IAiTranslationService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GeminiTranslationService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Gemini:ApiKey"]!;
    }

    public async Task<string> GetAiResponse(string prompt) // Đổi tên cho tổng quát
{
    var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

    var payload = new
    {
        contents = new[]
        {
            new { parts = new[] { new { text = prompt } } } // Gửi trực tiếp prompt từ Controller
        }
    };

    var json = JsonSerializer.Serialize(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await _http.PostAsync(url, content);
    
    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadAsStringAsync();

    using var doc = JsonDocument.Parse(result);
    return doc.RootElement.GetProperty("candidates")[0]
              .GetProperty("content")
              .GetProperty("parts")[0]
              .GetProperty("text").GetString()!;
}
}

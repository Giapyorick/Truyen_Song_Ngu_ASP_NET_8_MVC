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
        _apiKey = config["Groq:ApiKey"] ?? throw new ArgumentNullException("Chưa cấu hình Groq:ApiKey trong appsettings.json!");
    }

    public async Task<string> GetAiResponse(string prompt)
    {
        var url = "https://api.groq.com/openai/v1/chat/completions";

        var payload = new
        {
            model = "llama-3.3-70b-versatile",
            messages = new[]
            {
                new { role = "system", content = "You are a professional translator for stories and comics. Translate the input accurately and naturally." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Lỗi Groq API [{response.StatusCode}]: {result}");
        }

        using var doc = JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content").GetString() ?? "";
    }
}
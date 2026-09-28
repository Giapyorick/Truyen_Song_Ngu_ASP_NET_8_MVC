using Microsoft.AspNetCore.Mvc;
using WebTruyenTranh.Helpers;
using System.Text.Json.Serialization;

[Route("Ai/[action]")]
public class AiController : Controller
{
    private readonly IAiTranslationService _ai;

    public AiController(IAiTranslationService ai)
    {
        _ai = ai;
    }

    [HttpPost]
    public async Task<IActionResult> Translate([FromBody] TranslateRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(new { message = "Please enter the content to be translated." });

        try
        {
            var prompt = $"Translate the following text to Vietnamese naturally, return only the translation:\n\n{req.Text}";
            var translated = await _ai.GetAiResponse(prompt);
            return Json(new { translatedText = translated });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"AI Service Error: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Explain([FromBody] TranslateRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(new { message = "Please enter a word/phrase to explain." });

        try
        {
            var prompt = $@"
                Bạn là một chuyên gia ngôn ngữ học. Hãy giải thích từ/cụm từ: '{req.Text}'
                Hãy trả về nội dung theo định dạng Markdown đẹp mắt với các icon sau:

                ### **Nghĩa tiếng Việt**
                > **[Nghĩa chính ở đây]**

                ---
                ### **Giải thích chi tiết**
                * **Sắc thái:** [Giải thích về cảm xúc, hoàn cảnh dùng]
                * **Nguồn gốc (nếu thú vị):** [Giải thích ngắn gọn]

                ---
                ### **Ví dụ minh họa**
                * 🇬🇧 **English:** [Câu ví dụ tiếng Anh]
                * 🇻🇳 **Tiếng Việt:** [Dịch nghĩa câu ví dụ]
                
                *(Lưu ý: Luôn sử dụng đậm nhạt và danh sách để dễ đọc)*";

            var result = await _ai.GetAiResponse(prompt);
            return Json(new { result = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"AI Service Error: {ex.Message}" });
        }
    }
}

public class TranslateRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
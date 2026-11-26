using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.Controllers
{
    [Route("Ai/[action]")]
    public class AiController : Controller
    {
        private readonly IAiTranslationService _ai;
        private readonly TruyenSongNguContext _context;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public AiController(
            IAiTranslationService ai,
            TruyenSongNguContext context,
            IStringLocalizer<SharedResource> localizer)
        {
            _ai = ai;
            _context = context;
            _localizer = localizer;
        }

        [HttpPost]
        public async Task<IActionResult> Translate([FromBody] TranslateRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Text))
                return BadRequest(new { message = _localizer["Ai_EnterContentToTranslate"].Value });

            try
            {
                var prompt = $"Translate the following text to Vietnamese naturally, return only the translation:\n\n{req.Text}";
                var translated = await _ai.GetAiResponse(prompt);
                return Json(new { translatedText = translated });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"{_localizer["Ai_ServiceError"].Value}: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Explain([FromBody] AiRequestModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Text))
                return BadRequest(new { message = _localizer["Ai_TextCannotBeEmpty"].Value });

            bool isVi = string.IsNullOrWhiteSpace(model.Culture)
                ? CultureInfo.CurrentUICulture.Name.StartsWith("vi", StringComparison.OrdinalIgnoreCase)
                : model.Culture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

            string contextInfo = "";
            if (model.ChapterId.HasValue && model.ChapterId.Value > 0)
            {
                var chapter = await _context.TblChapters
                    .Include(c => c.Story)
                    .FirstOrDefaultAsync(c => c.ChapterId == model.ChapterId.Value);

                if (chapter != null)
                {
                    contextInfo = isVi
                        ? $"[Bối cảnh truyện: \"{chapter.Story?.Title}\", Chương: \"{chapter.Title}\"]\n"
                        : $"[Story context: \"{chapter.Story?.Title}\", Chapter: \"{chapter.Title}\"]\n";
                }
            }

            string prompt = isVi
                ? $"{contextInfo}Bạn là gia sư ngoại ngữ và trợ lý đọc truyện song ngữ. Hãy giải thích ngắn gọn, dễ hiểu từ/câu sau bằng TIẾNG VIỆT theo đúng ngữ cảnh văn học:\n" +
                  $"Đoạn cần giải thích: \"{model.Text}\"\n" +
                  $"Yêu cầu:\n- Dịch sát nghĩa theo bối cảnh truyện.\n- Giải thích ngắn gọn từ vựng/ngữ pháp nổi bật.\n- Đưa ra 1 ví dụ thực tế kèm bản dịch."
                : $"{contextInfo}You are a language tutor and bilingual reading assistant. Briefly and clearly explain the following text in ENGLISH within the story's literary context:\n" +
                  $"Text to explain: \"{model.Text}\"\n" +
                  $"Requirements:\n- Provide an accurate contextual translation/meaning.\n- Highlight key vocabulary/grammar structures.\n- Give 1 practical example sentence with meaning.";

            try
            {
                string aiReply = await _ai.GetAiResponse(prompt);
                return Json(new { result = aiReply });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost]
        public async Task StreamExplain([FromBody] AiRequestModel model, CancellationToken cancellationToken)
        {
            var bufferingFeature = HttpContext.Features.Get<IHttpResponseBodyFeature>();
            bufferingFeature?.DisableBuffering();

            Response.Headers.Append("Content-Type", "text/event-stream; charset=utf-8");
            Response.Headers.Append("Cache-Control", "no-cache, no-transform");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no");

            if (model == null || string.IsNullOrWhiteSpace(model.Text))
            {
                await Response.WriteAsync("data: \"[DONE]\"\n\n", cancellationToken);
                return;
            }

            bool isVi = string.IsNullOrWhiteSpace(model.Culture)
                ? CultureInfo.CurrentUICulture.Name.StartsWith("vi", StringComparison.OrdinalIgnoreCase)
                : model.Culture.StartsWith("vi", StringComparison.OrdinalIgnoreCase);

            string contextInfo = "";
            string chapterFullContent = "";

            if (model.ChapterId.HasValue && model.ChapterId.Value > 0)
            {
                var chapter = await _context.TblChapters
                    .Include(c => c.Story)
                    .Include(c => c.TblParagraphs)
                    .FirstOrDefaultAsync(c => c.ChapterId == model.ChapterId.Value, cancellationToken);

                if (chapter != null)
                {
                    contextInfo = isVi
                        ? $"[Bối cảnh: Truyện \"{chapter.Story?.Title}\", Chương \"{chapter.Title}\"]\n"
                        : $"[Context: Story \"{chapter.Story?.Title}\", Chapter \"{chapter.Title}\"]\n";

                    if (chapter.TblParagraphs != null && chapter.TblParagraphs.Any())
                    {
                        var englishParagraphs = chapter.TblParagraphs
                            .OrderBy(p => p.ParagraphId)
                            .Select(p => p.English)
                            .Where(t => !string.IsNullOrWhiteSpace(t));

                        chapterFullContent = string.Join("\n\n", englishParagraphs);
                    }

                    if (chapterFullContent.Length > 4000)
                    {
                        chapterFullContent = chapterFullContent.Substring(0, 4000) + "...";
                    }
                }
            }

            string analysisText = !string.IsNullOrWhiteSpace(model.Text)
                ? model.Text.Trim()
                : chapterFullContent;

            string prompt = "";
            switch (model.Mode?.ToLower())
            {
                case "summary":
                    prompt = isVi
                        ? $"{contextInfo}Dưới đây là nội dung chương truyện:\n\"\"\"{analysisText}\"\"\"\n" +
                          "Hãy TÓM TẮT DIỄN BIẾN TOÀN CHƯƠNG thật lôi cuốn, dễ hiểu. Quy tắc: KHÔNG dùng emoji. BẮT BUỘC dùng đúng các thẻ sau:\n\n" +
                          "### <span class=\"badge-pill bg-info\"><i class=\"fa-solid fa-bolt\"></i> Diễn biến chính</span>\n" +
                          "- Tóm tắt 3-4 gạch đầu dòng các sự kiện cốt lõi diễn ra trong chương.\n\n" +
                          "### <span class=\"badge-pill bg-cus\"><i class=\"fa-solid fa-user-tag\"></i> Tình thế nhân vật</span>\n" +
                          "- Nhân vật chính đang đối mặt với thử thách hoặc quyết định gì."
                        : $"{contextInfo}Here is the chapter content:\n\"\"\"{analysisText}\"\"\"\n" +
                          "SUMMARIZE THE CHAPTER EVENTS concisely. Rules: NO emojis. MUST use these exact tags:\n\n" +
                          "### <span class=\"badge-pill bg-info\"><i class=\"fa-solid fa-bolt\"></i> Main Events</span>\n" +
                          "- 3-4 bullet points outlining key plot developments.\n\n" +
                          "### <span class=\"badge-pill bg-cus\"><i class=\"fa-solid fa-user-tag\"></i> Character Status</span>\n" +
                          "- Core conflicts or decisions characters are facing.";
                    break;

                case "lore":
                    prompt = isVi
                        ? $"{contextInfo}Dưới đây là nội dung chương truyện:\n\"\"\"{analysisText}\"\"\"\n" +
                          "Hãy GIẢI MÃ CỐT TRUYỆN, ẨN Ý & THUẬT NGỮ trong chương này. Quy tắc: KHÔNG dùng emoji. BẮT BUỘC dùng đúng các thẻ sau:\n\n" +
                          "### <span class=\"badge-pill bg-main\"><i class=\"fa-solid fa-compass\"></i> Nút thắt & Ẩn ý</span>\n" +
                          "- Bóc tách ý đồ tác giả, chi tiết báo trước (foreshadowing) hoặc xung đột ngầm.\n\n" +
                          "### <span class=\"badge-pill bg-wait\"><i class=\"fa-solid fa-scroll\"></i> Thuật ngữ & Bối cảnh thế giới</span>\n" +
                          "- Giải thích các khái niệm, gia tộc, địa danh hoặc điển cố quan trọng xuất hiện."
                        : $"{contextInfo}Here is the chapter content:\n\"\"\"{analysisText}\"\"\"\n" +
                          "DECODE LORE, SUBTEXT & KEY TERMS from this chapter. Rules: NO emojis. MUST use these exact tags:\n\n" +
                          "### <span class=\"badge-pill bg-main\"><i class=\"fa-solid fa-compass\"></i> Plot Subtext</span>\n" +
                          "- Hidden motivations, foreshadowing, and atmosphere.\n\n" +
                          "### <span class=\"badge-pill bg-wait\"><i class=\"fa-solid fa-scroll\"></i> World Lore & Terms</span>\n" +
                          "- Key organizations, archaic terms, or fictional concepts.";
                    break;

                case "feelings":
                    prompt = isVi
                        ? $"{contextInfo}Dưới đây là nội dung chương truyện:\n\"\"\"{analysisText}\"\"\"\n" +
                          "Hãy chia sẻ CẢM NHẬN VĂN HỌC & ĐỒNG HÀNH ĐỌC TRUYỆN. Quy tắc: KHÔNG dùng emoji. BẮT BUỘC dùng đúng các thẻ sau:\n\n" +
                          "### <span class=\"badge-pill bg-main\"><i class=\"fa-solid fa-book-open-reader\"></i> Cảm thụ chương này</span>\n" +
                          "(Nhận xét ngắn về nhịp truyện, cao trào cảm xúc)\n\n" +
                          "### <span class=\"badge-pill bg-cus\"><i class=\"fa-solid fa-masks-theater\"></i> Chiều sâu nhân vật</span>\n" +
                          "- Đánh giá diễn biến tâm lý và thay đổi của nhân vật.\n\n" +
                          "### <span class=\"badge-pill bg-accepted\"><i class=\"fa-solid fa-quote-right\"></i> Câu thoại / Trích dẫn đắt giá</span>\n" +
                          "- *Trích dẫn nguyên văn* -> Ý nghĩa sâu sắc."
                        : $"{contextInfo}Chapter passage:\n\"\"\"{analysisText}\"\"\"\n" +
                          "Share LITERARY APPRECIATION & READING COMPANIONSHIP. Rules: NO emojis. MUST use these exact tags:\n\n" +
                          "### <span class=\"badge-pill bg-main\"><i class=\"fa-solid fa-book-open-reader\"></i> Chapter Impression</span>\n" +
                          "(Emotional peaks and narrative pacing)\n\n" +
                          "### <span class=\"badge-pill bg-cus\"><i class=\"fa-solid fa-masks-theater\"></i> Character Psychology</span>\n" +
                          "- Emotional depth and relationship developments.\n\n" +
                          "### <span class=\"badge-pill bg-accepted\"><i class=\"fa-solid fa-quote-right\"></i> Memorable Quote</span>\n" +
                          "- *Quote line* -> Contextual significance.";
                    break;

                case "grammar":
                    prompt = isVi
                        ? $"{contextInfo}Phân tích NGỮ PHÁP chi tiết cho câu: \"{analysisText}\"\n" +
                          "Quy tắc: KHÔNG dùng emoji. BẮT BUỘC dùng đúng các thẻ sau:\n\n" +
                          "### <span class=\"badge-pill bg-info\"><i class=\"fa-solid fa-layer-group\"></i> Cấu trúc câu</span>\n" +
                          "- Phân tích thành phần S + V + O và mệnh đề.\n\n" +
                          "### <span class=\"badge-pill bg-cus\"><i class=\"fa-solid fa-thumbtack\"></i> Ngữ pháp trọng tâm</span>\n" +
                          "- Cấu trúc, thì và mẫu câu đáng nhớ.\n\n" +
                          "### <span class=\"badge-pill bg-accepted\"><i class=\"fa-solid fa-circle-check\"></i> Ví dụ minh họa</span>\n" +
                          "- *Câu ví dụ* -> Bản dịch."
                        : $"{contextInfo}Analyze GRAMMAR for: \"{analysisText}\"\n" +
                          "Rules: NO emojis. MUST use these exact tags:\n\n" +
                          "### <span class=\"badge-pill bg-info\"><i class=\"fa-solid fa-layer-group\"></i> Structure</span>\n" +
                          "- Clauses, subject, verb, and object breakdown.\n\n" +
                          "### <span class=\"badge-pill bg-cus\"><i class=\"fa-solid fa-thumbtack\"></i> Core Rules</span>\n" +
                          "- Grammar patterns in context.";
                    break;

                default:
                    prompt = isVi
                        ? $"{contextInfo}Giải thích đoạn văn sau theo đúng bối cảnh truyện:\n\"{analysisText}\"\n\n" +
                          "Quy tắc: KHÔNG dùng emoji. BẮT BUỘC dùng đúng các thẻ sau:\n\n" +
                          "### <span class=\"badge-pill bg-main\"><i class=\"fa-solid fa-align-left\"></i> Ý nghĩa trong cảnh này</span>\n" +
                          "(1-2 câu giải thích nghĩa sát nhất với câu chuyện)\n\n" +
                          "### <span class=\"badge-pill bg-cus\"><i class=\"fa-solid fa-star\"></i> Điểm nổi bật & Từ ngữ</span>\n" +
                          "- **Từ/Thành ngữ/Ẩn ý**: giải nghĩa cụ thể.\n\n" +
                          "### <span class=\"badge-pill bg-accepted\"><i class=\"fa-solid fa-feather-pointed\"></i> Diễn đạt mượt mà</span>\n" +
                          "- Bản dịch văn học thuần Việt giàu cảm xúc."
                        : $"{contextInfo}Explain this passage in literary context:\n\"{analysisText}\"\n\n" +
                          "Rules: NO emojis. MUST use these exact tags:\n\n" +
                          "### <span class=\"badge-pill bg-main\"><i class=\"fa-solid fa-align-left\"></i> Contextual Meaning</span>\n" +
                          "(1-2 concise sentences)\n\n" +
                          "### <span class=\"badge-pill bg-cus\"><i class=\"fa-solid fa-star\"></i> Highlights</span>\n" +
                          "- Nuances, idioms, and phrases.\n\n" +
                          "### <span class=\"badge-pill bg-accepted\"><i class=\"fa-solid fa-feather-pointed\"></i> Literary Rendition</span>\n" +
                          "- Fluid narrative translation.";
                    break;
            }

            try
            {
                await foreach (var token in _ai.StreamAiResponseAsync(prompt, cancellationToken))
                {
                    string jsonToken = JsonSerializer.Serialize(token);
                    await Response.WriteAsync($"data: {jsonToken}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                await Response.WriteAsync("data: \"[DONE]\"\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                string jsonErr = JsonSerializer.Serialize($"\n[{_localizer["Ai_ErrorPrefix"].Value}: {ex.Message}]");
                await Response.WriteAsync($"data: {jsonErr}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveVocabulary([FromBody] SaveVocabRequest req)
        {
            try
            {
                int currentUserId = 0;

                var claimId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(claimId) && int.TryParse(claimId, out int parsedClaimId))
                {
                    currentUserId = parsedClaimId;
                }

                if (currentUserId == 0)
                {
                    currentUserId = HttpContext.Session.GetInt32("UserId")
                                    ?? HttpContext.Session.GetInt32("AdminId")
                                    ?? HttpContext.Session.GetInt32("AccountId")
                                    ?? 0;
                }

                if (currentUserId == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = _localizer["Ai_RequireLoginToSave"].Value
                    });
                }

                if (req == null || string.IsNullOrWhiteSpace(req.Word))
                {
                    return Json(new
                    {
                        success = false,
                        message = _localizer["Ai_ContentCannotBeEmpty"].Value
                    });
                }

                string cleanWord = req.Word.Trim();
                if (cleanWord.Length > 200)
                {
                    cleanWord = cleanWord.Substring(0, 197) + "...";
                }

                int? validChapterId = null;
                if (req.ChapterId.HasValue && req.ChapterId.Value > 0)
                {
                    bool chapterExists = await _context.TblChapters.AnyAsync(c => c.ChapterId == req.ChapterId.Value);
                    if (chapterExists)
                    {
                        validChapterId = req.ChapterId.Value;
                    }
                }

                var entity = new TblUserVocabulary
                {
                    UserId = currentUserId,
                    WordOrPhrase = cleanWord,
                    Explanation = req.Explanation?.Trim() ?? "",
                    ContextSentence = req.ContextSentence?.Trim(),
                    ChapterId = validChapterId,
                    CreatedAt = DateTime.Now
                };

                _context.TblUserVocabularies.Add(entity);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = _localizer["Ai_SaveSuccess"].Value
                });
            }
            catch (DbUpdateException dbEx)
            {
                var innerMsg = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
                return Json(new { success = false, message = $"{_localizer["Ai_DatabaseError"].Value}: {innerMsg}" });
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"{_localizer["Ai_ServerError"].Value}: {innerMsg}" });
            }
        }
    }

    public class AiRequestModel
    {
        public string Text { get; set; } = string.Empty;
        public int? ChapterId { get; set; }
        public string? Culture { get; set; }
        public string Mode { get; set; } = "Explain";
    }

    public class TranslateRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class SaveVocabRequest
    {
        public string Word { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public string? ContextSentence { get; set; }
        public int? ChapterId { get; set; }
    }
}
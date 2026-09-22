using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.Services
{
    public interface IParagraphAiProcessingService
    {
        Task ProcessChapterFileJob(int chapterId, string tempFilePath, string targetLanguage);
    }

    public class ParagraphAiProcessingService : IParagraphAiProcessingService
    {
        private readonly TruyenSongNguContext _context;
        private readonly IAiTranslationService _aiService;
        private readonly ILogger<ParagraphAiProcessingService> _logger;

        public ParagraphAiProcessingService(
            TruyenSongNguContext context,
            IAiTranslationService aiService,
            ILogger<ParagraphAiProcessingService> logger)
        {
            _context = context;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task ProcessChapterFileJob(int chapterId, string tempFilePath, string targetLanguage)
        {
            try
            {
                if (!File.Exists(tempFilePath))
                {
                    _logger.LogError($"[Hangfire] File tạm không tồn tại: {tempFilePath}");
                    return;
                }

                // ==========================================
                // BƯỚC 1: C# & REGEX TỰ ĐỘNG BÓC TÁCH VÀ GÁN BLOCKTYPE
                // ==========================================
                string rawContent = ReadRawTextFromFile(tempFilePath);
                var segmentedBlocks = SegmentTextToBlocks(rawContent);

                if (segmentedBlocks.Count == 0)
                {
                    _logger.LogWarning($"[Hangfire] File {tempFilePath} rỗng sau khi bóc tách.");
                    return;
                }

                int currentMaxOrder = await _context.TblParagraphs
                    .Where(p => p.ChapterId == chapterId)
                    .Select(p => (int?)p.ParagraphOrder)
                    .MaxAsync() ?? -1;

                int orderTracker = currentMaxOrder + 1;

                // Nếu là câu đầu tiên của cả chapter chưa có dòng nào
                if (currentMaxOrder == -1 && segmentedBlocks.Count > 0)
                {
                    segmentedBlocks[0].BlockType = 0;
                }

                // Danh sách ngôn ngữ cần dịch
                var requestedLangs = (targetLanguage ?? "Vietnamese")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(l => l.Trim())
                    .Distinct()
                    .ToList();

                // ==========================================
                // BƯỚC 2: GỌI AI DỊCH VÀ LƯU DỮ LIỆU ĐA NGỮ
                // ==========================================
                int batchSize = 4;
                for (int i = 0; i < segmentedBlocks.Count; i += batchSize)
                {
                    var currentBatch = segmentedBlocks.Skip(i).Take(batchSize).ToList();

                    var inputItems = currentBatch.Select((b, idx) => new { id = idx + 1, text = b.English }).ToList();
                    string jsonInput = JsonSerializer.Serialize(inputItems);

                    var langInstructions = new StringBuilder();
                    foreach (var lang in requestedLangs)
                    {
                        string langKey = NormalizeLangProperty(lang);
                        langInstructions.AppendLine($"- Dịch 'text' sang {lang}, gán vào key: \"{langKey}\"");
                    }

                    string prompt = $@"Translate the following English lines into: {string.Join(", ", requestedLangs)}.

RULES:
{langInstructions}

INPUT DATA:
{jsonInput}

OUTPUT FORMAT:
Return a JSON array of objects. Strictly NO markdown code fences, NO explanation.
Format:
[
  {{
    ""id"": 1,
    {string.Join(",\n    ", requestedLangs.Select(l => $"\"{NormalizeLangProperty(l)}\": \"...\""))}
  }}
]";

                    // 1. Gọi AI - Nếu lỗi phải ném ra để thấy trên Hangfire, không nuốt lỗi
                    string aiResponse = "";
                    try
                    {
                        aiResponse = await _aiService.GetAiResponse(prompt);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[Hangfire] Lỗi gọi AI batch {i / batchSize + 1}: {ex.Message}");
                        throw new Exception($"[Lỗi gọi Groq API ở batch {i / batchSize + 1}]: {ex.Message}", ex);
                    }

                    // 2. Làm sạch Markdown & thẻ suy luận
                    string cleanJson = Regex.Replace(aiResponse ?? "", @"<think>[\s\S]*?<\/think>", "", RegexOptions.IgnoreCase).Trim();
                    if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) cleanJson = cleanJson.Substring(7);
                    else if (cleanJson.StartsWith("```")) cleanJson = cleanJson.Substring(3);
                    if (cleanJson.EndsWith("```")) cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
                    cleanJson = cleanJson.Trim();

                    int firstBracket = cleanJson.IndexOf('[');
                    int lastBracket = cleanJson.LastIndexOf(']');
                    if (firstBracket >= 0 && lastBracket > firstBracket)
                    {
                        cleanJson = cleanJson.Substring(firstBracket, lastBracket - firstBracket + 1);
                    }

                    // 3. Parse JSON với cơ chế fallback theo chỉ số mảng (Index)
                    var translationsList = new List<JsonElement>();
                    try
                    {
                        if (string.IsNullOrWhiteSpace(cleanJson) || (!cleanJson.Contains("[") && !cleanJson.Contains("{")))
                        {
                            _logger.LogError($"[Hangfire] Groq trả về chuỗi rỗng hoặc không có JSON. Raw: {aiResponse}");
                            throw new Exception($"Groq không sinh được nội dung dịch cho batch này (chuỗi trả về rỗng). Vui lòng kiểm tra lại quota hoặc đổi model.");
                        }
                        using var doc = JsonDocument.Parse(cleanJson);
                        JsonElement targetArray = default;

                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            targetArray = doc.RootElement;
                        }
                        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.Array)
                                {
                                    targetArray = prop.Value;
                                    break;
                                }
                            }
                        }

                        if (targetArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var elem in targetArray.EnumerateArray())
                            {
                                translationsList.Add(elem.Clone());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[Hangfire] Lỗi Parse JSON batch {i / batchSize + 1}: {ex.Message} | Raw: {cleanJson}");
                        throw new Exception($"[Lỗi cấu trúc JSON từ AI trả về]: {ex.Message}. Chuỗi AI trả về: {cleanJson}", ex);
                    }

                    // 4. Hàm trích xuất chuỗi đa ngữ (quét linh hoạt mọi key)
                    string ExtractLang(JsonElement elem, params string[] patterns)
                    {
                        if (elem.ValueKind != JsonValueKind.Object) return null;

                        foreach (var prop in elem.EnumerateObject())
                        {
                            string name = prop.Name.ToLower();
                            foreach (var p in patterns)
                            {
                                if (name == p.ToLower() || name.Contains(p.ToLower()))
                                {
                                    if (prop.Value.ValueKind == JsonValueKind.String)
                                    {
                                        string str = prop.Value.GetString();
                                        if (!string.IsNullOrWhiteSpace(str)) return str.Trim();
                                    }
                                }
                            }
                        }
                        return null;
                    }

                    // 5. Lưu vào Database (Khớp dữ liệu theo vị trí tuần tự của câu)
                    var newEntities = new List<TblParagraph>();
                    for (int k = 0; k < currentBatch.Count; k++)
                    {
                        var block = currentBatch[k];
                        string vi = null, zh = null, ja = null, fr = null;

                        // Ưu tiên lấy theo vị trí index k trong mảng trả về
                        if (k < translationsList.Count)
                        {
                            var transElem = translationsList[k];
                            vi = ExtractLang(transElem, "vietnamese", "tieng_viet", "vi", "vie");
                            zh = ExtractLang(transElem, "chinese", "tieng_trung", "zh", "cn", "zho");
                            ja = ExtractLang(transElem, "japanese", "tieng_nhat", "ja", "jp", "jpn");
                            fr = ExtractLang(transElem, "french", "tieng_phap", "fr", "fra");
                        }

                        newEntities.Add(new TblParagraph
                        {
                            ChapterId = chapterId,
                            ParagraphOrder = orderTracker++,
                            BlockType = block.BlockType,
                            English = block.English,
                            Vietnamese = vi ?? "",
                            Chinese = zh,
                            Japanese = ja,
                            French = fr
                        });
                    }

                    await _context.TblParagraphs.AddRangeAsync(newEntities);
                    await _context.SaveChangesAsync();

                    await Task.Delay(2500); // Nghỉ nhẹ tránh nghẽn request
                }

                // Đánh lại STT liên tục
                await _context.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_ReindexParagraphOrder @ChapID = {chapterId}");
                _logger.LogInformation($"[Hangfire] Hoàn tất xử lý & dịch Chapter ID: {chapterId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Hangfire] Lỗi xử lý Job: {ex.Message}");
                throw;
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
            }
        }

        /// <summary>
        /// Thuật toán C# + Regex tách thoại và gán BlockType cực nhanh và chuẩn xác 100%
        /// </summary>
        private static List<ExtractedBlock> SegmentTextToBlocks(string rawText)
        {
            var result = new List<ExtractedBlock>();
            if (string.IsNullOrWhiteSpace(rawText)) return result;

            // 1. Làm sạch sơ bộ các lỗi ngắt từ OCR
            rawText = SanitizeOcrArtifacts(rawText);

            // 2. Chuẩn hóa ký tự xuống dòng
            rawText = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

            // 3. Tách theo ĐOẠN VĂN THỰC SỰ (2 dấu xuống dòng liên tiếp)
            var rawParagraphs = Regex.Split(rawText, @"\n\s*\n+")
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            foreach (var para in rawParagraphs)
            {
                // 4. Ghép các dòng đơn lẻ trong cùng một đoạn văn thành một khối liền mạch
                // Đồng thời hàn gắn các từ bị đứt đôi (ví dụ: "misdemean\nours" -> "misdemeanours")
                string cleanPara = Regex.Replace(para, @"(\b\w+)-?\n+(\w+\b)", "$1$2"); // Hàn từ bị ngắt dòng
                cleanPara = Regex.Replace(cleanPara, @"\s*\n+\s*", " ").Trim();          // Ghép dòng thành khoảng trắng
                cleanPara = Regex.Replace(cleanPara, @"[ ]{2,}", " ");                    // Xóa khoảng trắng thừa

                if (string.IsNullOrWhiteSpace(cleanPara)) continue;

                // 5. Tách đoạn văn thành các phần Lời thoại và Lời dẫn/Trần thuật
                // Chỉ nhận diện thoại khi nằm trong cặp ngoặc kép "..." hoặc “...” (KHÔNG dùng nháy đơn ' vì dính dấu sở hữu cách như priest’s)
                var dialogueRegex = new Regex(@"([“""][^”""]+?[”""])", RegexOptions.Compiled);
                var parts = dialogueRegex.Split(cleanPara)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                bool isFirstPartInParagraph = true;

                foreach (var part in parts)
                {
                    // Kiểm tra xem phần này có phải là câu thoại trong ngoặc kép không
                    bool isDialogue = (part.StartsWith("\"") && part.EndsWith("\"")) ||
                                      (part.StartsWith("“") && part.EndsWith("”"));

                    if (isDialogue)
                    {
                        // Chuẩn hóa về ngoặc kép tiêu chuẩn
                        string formattedQuote = "\"" + part.Substring(1, part.Length - 2).Trim() + "\"";

                        result.Add(new ExtractedBlock
                        {
                            BlockType = 4, // 4: Lời thoại
                            English = formattedQuote
                        });
                        isFirstPartInParagraph = false;
                    }
                    else
                    {
                        // Tách câu trần thuật dựa vào dấu chấm, hỏi, cảm (. ! ?) có khoảng trắng theo sau
                        var sentences = Regex.Split(part, @"(?<=[\.!\?][”""]?)\s+(?=[A-Z0-9“""])")
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();

                        foreach (var sentence in sentences)
                        {
                            int blockType;
                            if (isFirstPartInParagraph)
                            {
                                blockType = 2; // 2: Đầu đoạn văn mới (Thụt dòng)
                                isFirstPartInParagraph = false;
                            }
                            else
                            {
                                blockType = 1; // 1: Câu nối tiếp trong đoạn
                            }

                            result.Add(new ExtractedBlock
                            {
                                BlockType = blockType,
                                English = sentence
                            });
                        }
                    }
                }
            }

            return result;
        }

        private static string NormalizeLangProperty(string lang)
        {
            string l = lang.ToLower();
            if (l.Contains("vi")) return "vietnamese";
            if (l.Contains("zh") || l.Contains("cn")) return "chinese";
            if (l.Contains("ja") || l.Contains("jp")) return "japanese";
            if (l.Contains("fr")) return "french";
            return l;
        }

        private static string SanitizeOcrArtifacts(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            text = text.Replace("\t", " ");

            // Chuẩn hóa các dấu nháy đơn lộn xộn tránh làm hỏng sở hữu cách
            text = text.Replace("’", "'").Replace("`", "'");

            // Hàn gắn các từ đứt quãng cụ thể trong tài liệu scan
            text = Regex.Replace(text, @"\bmisdemean\s+ours\b", "misdemeanours", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bmain\s+tained\b", "maintained", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bsamo\s+vars\b", "samovars", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bex\s+erted\b", "exerted", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bpot-hellied\b", "pot-bellied", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bbuflfet\b", "buffet", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\blell\b", "tell", RegexOptions.IgnoreCase);

            // Chuẩn hóa vị trí dấu câu sát ngoặc kép
            text = Regex.Replace(text, @"([.!?])\s+([”""])", "$1$2");
            text = Regex.Replace(text, @"([“""])\s+", "$1");

            return text;
        }

        private string ReadRawTextFromFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            var sb = new StringBuilder();

            if (ext == ".txt")
            {
                using var reader = new StreamReader(filePath, Encoding.UTF8);
                return reader.ReadToEnd();
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var stream = File.OpenRead(filePath))
            using (var package = new ExcelPackage(stream))
            {
                var sheet = package.Workbook.Worksheets[0];
                int rowCount = sheet.Dimension?.Rows ?? 0;
                for (int row = 1; row <= rowCount; row++)
                {
                    string cell = sheet.Cells[row, 1].Text;
                    if (!string.IsNullOrWhiteSpace(cell))
                    {
                        sb.AppendLine(cell.Trim());
                    }
                }
            }
            return sb.ToString();
        }

        private class ExtractedBlock
        {
            public int BlockType { get; set; } = 1;
            public string English { get; set; } = "";
        }
    }
}
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.Services
{
    public interface IParagraphAiProcessingService
    {
        Task ProcessChapterFileJob(int chapterId, string tempFilePath, string targetLanguage);
        Task ProcessAiAlignAndImportJob(
            int chapterId,
            string tempEnPath,
            string? tempViPath,
            string? tempZhPath,
            string? tempJaPath,
            string? tempFrPath);
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
                    _logger.LogError($"[Hangfire] Temporary file not found: {tempFilePath}");
                    return;
                }

                string rawContent = ReadRawTextFromFile(tempFilePath);
                var allBlocks = SegmentTextToBlocks(rawContent);

                if (allBlocks.Count == 0)
                {
                    _logger.LogWarning($"[Hangfire] File {tempFilePath} is empty after segmentation.");
                    return;
                }

                var requestedLangs = (targetLanguage ?? "Vietnamese")
                    .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(l => l.Trim())
                    .Where(l => !l.Equals("English", StringComparison.OrdinalIgnoreCase) &&
                                !l.Equals("en", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .ToList();

                if (requestedLangs.Count == 0)
                {
                    requestedLangs.Add("Vietnamese");
                }

                var (chunkSize, delayMs) = CalculateBatchConfig(requestedLangs.Count);
                _logger.LogInformation($"[Hangfire Config] Translating {requestedLangs.Count} language(s). Batch: {chunkSize}, Delay: {delayMs / 1000.0}s.");

                // LẤY THỨ TỰ LỚN NHẤT HIỆN TẠI TRONG CSDL ĐỂ NỐI TIẾP (APPEND)
                int currentMaxOrder = await _context.TblParagraphs
                    .Where(p => p.ChapterId == chapterId)
                    .Select(p => (int?)p.ParagraphOrder)
                    .MaxAsync() ?? -1;

                int orderTracker = currentMaxOrder >= 0 ? currentMaxOrder + 1 : 0;

                // Tiến hành chia batch và gửi AI dịch từng phần của file MỚI
                int processedInFileCount = 0;

                while (processedInFileCount < allBlocks.Count)
                {
                    var currentBatch = allBlocks.Skip(processedInFileCount).Take(chunkSize).ToList();
                    if (currentBatch.Count == 0) break;

                    // CHỈ gán BlockType = 0 (mở đầu chương) nếu chương này HOÀN TOÀN CHƯA CÓ DỮ LIỆU CŨ
                    if (currentMaxOrder == -1 && processedInFileCount == 0)
                    {
                        currentBatch[0].BlockType = 0;
                    }

                    _logger.LogInformation($"[Hangfire] Translating AI batch: #{processedInFileCount + 1} to #{processedInFileCount + currentBatch.Count} / {allBlocks.Count}");

                    var inputItems = currentBatch.Select((b, idx) => new { id = idx + 1, text = b.English }).ToList();
                    string jsonInput = JsonSerializer.Serialize(inputItems);

                    var langInstructions = new StringBuilder();
                    foreach (var lang in requestedLangs)
                    {
                        string langKey = NormalizeLangProperty(lang);
                        langInstructions.AppendLine($"- Translate 'text' into {lang}, assign to key: \"{langKey}\"");
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

                    string aiResponse = "";
                    bool callSuccess = false;

                    while (!callSuccess)
                    {
                        try
                        {
                            aiResponse = await _aiService.GetAiResponse(prompt);
                            if (!string.IsNullOrWhiteSpace(aiResponse))
                            {
                                callSuccess = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            string errMsg = ex.Message;
                            _logger.LogWarning($"[Hangfire] AI API message: {errMsg}");

                            if (errMsg.Contains("Rate limit", StringComparison.OrdinalIgnoreCase) ||
                                errMsg.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
                                errMsg.Contains("TPM", StringComparison.OrdinalIgnoreCase) ||
                                errMsg.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase))
                            {
                                double waitSeconds = 8.0;
                                var match = Regex.Match(errMsg, @"try again in (\d+(\.\d+)?)s", RegexOptions.IgnoreCase);
                                if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedSec))
                                {
                                    waitSeconds = parsedSec + 2.0;
                                }

                                _logger.LogInformation($"[Hangfire Throttling] TPM limit reached. Waiting {waitSeconds:F1}s...");
                                await Task.Delay((int)(waitSeconds * 1000));
                            }
                            else
                            {
                                await Task.Delay(4000);
                            }
                        }
                    }

                    string cleanJson = Regex.Replace(aiResponse ?? "", @"<think>[\s\S]*?<\/think>", "", RegexOptions.IgnoreCase).Trim();
                    if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) cleanJson = cleanJson.Substring(7);
                    else if (cleanJson.StartsWith("```")) cleanJson = cleanJson.Substring(3);
                    if (cleanJson.EndsWith("```")) cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
                    cleanJson = cleanJson.Trim();

                    int firstBracket = cleanJson.IndexOf('[');
                    if (firstBracket >= 0) cleanJson = cleanJson.Substring(firstBracket);

                    cleanJson = RepairTruncatedJsonArray(cleanJson);

                    var translationsList = new List<JsonElement>();
                    try
                    {
                        using var doc = JsonDocument.Parse(cleanJson);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var elem in doc.RootElement.EnumerateArray())
                            {
                                translationsList.Add(elem.Clone());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[Hangfire] JSON format error: {ex.Message}. Response: {cleanJson}");
                        await Task.Delay(3000);
                        continue;
                    }

                    int validCount = Math.Min(currentBatch.Count, translationsList.Count);
                    if (validCount == 0)
                    {
                        await Task.Delay(3000);
                        continue;
                    }

                    var newEntities = new List<TblParagraph>();
                    for (int k = 0; k < validCount; k++)
                    {
                        var block = currentBatch[k];
                        var transElem = translationsList[k];

                        string vi = ExtractLang(transElem, "vietnamese", "tieng_viet", "vi", "vie");
                        string zh = ExtractLang(transElem, "chinese", "tieng_trung", "zh", "cn", "zho");
                        string ja = ExtractLang(transElem, "japanese", "tieng_nhat", "ja", "jp", "jpn");
                        string fr = ExtractLang(transElem, "french", "tieng_phap", "fr", "fra");

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

                    processedInFileCount += validCount;

                    _logger.LogInformation($"[Hangfire] Saved {validCount} sentences. Delaying {delayMs / 1000.0}s...");
                    await Task.Delay(delayMs);
                }

                await _context.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_ReindexParagraphOrder @ChapID = {chapterId}");
                _logger.LogInformation($"[Hangfire] Finished processing Chapter ID: {chapterId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Hangfire] Job execution failed: {ex.Message}");
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

        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task ProcessAiAlignAndImportJob(
            int chapterId,
            string tempEnPath,
            string? tempViPath,
            string? tempZhPath,
            string? tempJaPath,
            string? tempFrPath)
        {
            try
            {
                // 1. Phân tích file Tiếng Anh chuẩn
                string rawEnText = ReadRawTextFromFile(tempEnPath);
                var enBlocks = SegmentTextToBlocks(rawEnText);
                int targetCount = enBlocks.Count;

                if (targetCount == 0)
                {
                    _logger.LogWarning($"[Hangfire AutoAlign] File Nguồn Tiếng Anh rỗng.");
                    return;
                }

                // 2. Hàm gọi AI căn chỉnh từng ngôn ngữ (Tích hợp Smart Throttling / Dynamic Delay)
                async Task<List<string>?> AlignLangAsync(string langName, string? filePath)
                {
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

                    string rawText = ReadRawTextFromFile(filePath);
                    var rawBlocks = SegmentTextToBlocks(rawText);

                    if (rawBlocks.Count == targetCount)
                        return rawBlocks.Select(b => b.English).ToList();

                    var allAlignedResults = new List<string>();

                    // Giảm xuống 7 câu/batch để đảm bảo Requested Tokens < 3500, tránh đụng trần 8000 TPM
                    int batchSize = 7;
                    int totalBatches = (int)Math.Ceiling((double)targetCount / batchSize);

                    for (int b = 0; b < totalBatches; b++)
                    {
                        var currentEnBatch = enBlocks.Skip(b * batchSize).Take(batchSize).ToList();
                        int currentBatchCount = currentEnBatch.Count;

                        int rawStart = (int)Math.Round((double)b * rawBlocks.Count / totalBatches);
                        int rawTake = (int)Math.Round((double)(b + 1) * rawBlocks.Count / totalBatches) - rawStart;
                        var currentRawBatch = rawBlocks.Skip(rawStart).Take(Math.Max(rawTake, currentBatchCount)).ToList();

                        var sourceList = currentEnBatch.Select((x, idx) => new { id = idx + 1, en = x.English }).ToList();
                        var rawList = currentRawBatch.Select((x, idx) => new { raw_id = idx + 1, text = x.English }).ToList();

                        string prompt = $@"You are a JSON-only API. Align {langName} translation lines with English source.

ENGLISH SOURCE ({currentBatchCount} items):
{JsonSerializer.Serialize(sourceList)}

RAW {langName.ToUpper()} TRANSLATION LINES:
{JsonSerializer.Serialize(rawList)}

TASK:
Re-align, split, or merge the provided {langName} lines to MATCH EXACTLY the {currentBatchCount} English items 1-to-1.

RULES:
- Return ONLY a JSON array of {currentBatchCount} objects with keys ""id"" (1 to {currentBatchCount}) and ""text"".
- Absolutely NO markdown blocks, NO explanations, NO thinking tags.
Example:
[
  {{\""id\"": 1, \""text\"": \""...\""}},
  {{\""id\"": 2, \""text\"": \""...\""}}
]";

                        string aiResponse = "";
                        bool callSuccess = false;

                        // VÒNG LẶP RETRY VÔ HẠN VỚI CHIẾN THUẬT BẮT LỖI RATE LIMIT
                        while (!callSuccess)
                        {
                            try
                            {
                                aiResponse = await _aiService.GetAiResponse(prompt);
                                if (!string.IsNullOrWhiteSpace(aiResponse))
                                {
                                    callSuccess = true;
                                }
                                else
                                {
                                    _logger.LogWarning($"[Hangfire AutoAlign] AI trả về rỗng cho {langName} Batch {b + 1}/{totalBatches}. Thử lại sau 3s...");
                                    await Task.Delay(3000);
                                }
                            }
                            catch (Exception ex)
                            {
                                string errMsg = ex.Message;
                                _logger.LogWarning($"[Hangfire AutoAlign Exception] {langName} Batch {b + 1}: {errMsg}");

                                // Bắt chính xác lỗi Rate Limit từ Groq API / OpenAI
                                if (errMsg.Contains("Rate limit", StringComparison.OrdinalIgnoreCase) ||
                                    errMsg.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase) ||
                                    errMsg.Contains("TPM", StringComparison.OrdinalIgnoreCase) ||
                                    errMsg.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase))
                                {
                                    double waitSeconds = 15.0; // Mặc định chờ 15s

                                    // Lấy số giây chính xác từ câu thông báo "Please try again in 20.7825s"
                                    var matchSec = Regex.Match(errMsg, @"try again in (\d+(\.\d+)?)s", RegexOptions.IgnoreCase);
                                    if (matchSec.Success && double.TryParse(matchSec.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedSec))
                                    {
                                        waitSeconds = parsedSec + 2.0; // Bội hoàn thêm 2 giây cho an toàn
                                    }

                                    _logger.LogInformation($"[Hangfire AutoAlign Throttling] Chạm trần TPM. Tự động tạm dừng {waitSeconds:F1}s trước khi gửi lại...");
                                    await Task.Delay((int)(waitSeconds * 1000));
                                }
                                else
                                {
                                    await Task.Delay(4000);
                                }
                            }
                        }

                        // Dọn dẹp JSON
                        string cleanJson = Regex.Replace(aiResponse, @"<think>[\s\S]*?<\/think>", "", RegexOptions.IgnoreCase).Trim();
                        if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) cleanJson = cleanJson.Substring(7);
                        else if (cleanJson.StartsWith("```")) cleanJson = cleanJson.Substring(3);
                        if (cleanJson.EndsWith("```")) cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
                        cleanJson = cleanJson.Trim();

                        int firstBracket = cleanJson.IndexOf('[');
                        if (firstBracket >= 0) cleanJson = cleanJson.Substring(firstBracket);

                        cleanJson = RepairTruncatedJsonArray(cleanJson);

                        var batchList = new List<string>();
                        try
                        {
                            using var doc = JsonDocument.Parse(cleanJson);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var elem in doc.RootElement.EnumerateArray())
                                {
                                    if (elem.TryGetProperty("text", out var textProp))
                                        batchList.Add(textProp.GetString() ?? "");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"[Hangfire AutoAlign] Lỗi Parse JSON {langName} Batch {b + 1}: {ex.Message}. Chuỗi: {cleanJson}");
                            b--; // Lùi Batch lại để thử lại Batch này
                            await Task.Delay(3000);
                            continue;
                        }

                        if (batchList.Count != currentBatchCount)
                        {
                            _logger.LogWarning($"[Hangfire AutoAlign] Batch {b + 1} tệp {langName} trả về {batchList.Count}/{currentBatchCount} câu. Thử lại batch...");
                            b--; // Thử lại batch nếu lệch câu
                            await Task.Delay(3000);
                            continue;
                        }

                        allAlignedResults.AddRange(batchList);

                        // Nghỉ 3.5 giây giữa mỗi Batch để cơ chế TPM khôi phục lại bớt token
                        _logger.LogInformation($"[Hangfire AutoAlign] Hoàn tất Batch {b + 1}/{totalBatches} cho {langName}. Nghỉ 3.5s...");
                        await Task.Delay(3500);
                    }

                    return allAlignedResults;
                }

                // 3. Tiến hành căn chỉnh từng ngôn ngữ
                var viLines = await AlignLangAsync("Vietnamese", tempViPath);
                var zhLines = await AlignLangAsync("Chinese", tempZhPath);
                var jaLines = await AlignLangAsync("Japanese", tempJaPath);
                var frLines = await AlignLangAsync("French", tempFrPath);

                // 4. Lưu dữ liệu vào CSDL
                int currentMaxOrder = await _context.TblParagraphs
                    .Where(p => p.ChapterId == chapterId)
                    .Select(p => (int?)p.ParagraphOrder)
                    .MaxAsync() ?? -1;

                int orderTracker = currentMaxOrder + 1;
                var newEntities = new List<TblParagraph>();

                for (int i = 0; i < targetCount; i++)
                {
                    var block = enBlocks[i];
                    int finalBlockType = block.BlockType;
                    if (i == 0 && currentMaxOrder == -1) finalBlockType = 0;

                    newEntities.Add(new TblParagraph
                    {
                        ChapterId = chapterId,
                        ParagraphOrder = orderTracker++,
                        BlockType = finalBlockType,
                        English = block.English.Trim(),
                        Vietnamese = viLines != null && i < viLines.Count ? CleanLeadingDash(viLines[i]) : "",
                        Chinese = zhLines != null && i < zhLines.Count ? CleanLeadingDash(zhLines[i]) : null,
                        Japanese = jaLines != null && i < jaLines.Count ? CleanLeadingDash(jaLines[i]) : null,
                        French = frLines != null && i < frLines.Count ? CleanLeadingDash(frLines[i]) : null
                    });
                }

                await _context.TblParagraphs.AddRangeAsync(newEntities);
                await _context.SaveChangesAsync();
                await _context.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_ReindexParagraphOrder @ChapID = {chapterId}");

                _logger.LogInformation($"[Hangfire AutoAlign] Hoàn thành căn chỉnh & lưu thành công {newEntities.Count} câu cho Chapter ID: {chapterId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Hangfire AutoAlign Failed] Lỗi: {ex.Message}");
                throw;
            }
            finally
            {
                TryDeleteFile(tempEnPath); TryDeleteFile(tempViPath); TryDeleteFile(tempZhPath);
                TryDeleteFile(tempJaPath); TryDeleteFile(tempFrPath);
            }
        }

        private static string CleanLeadingDash(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return Regex.Replace(text.Trim(), @"^[–-]\s*", "").Trim();
        }

        private void TryDeleteFile(string? path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
        }

        private static (int chunkSize, int delayMs) CalculateBatchConfig(int langCount)
        {
            return langCount switch
            {
                1 => (12, 2500),
                2 => (7, 3500),
                3 => (4, 4500),
                _ => (3, 6000)
            };
        }

        private static string RepairTruncatedJsonArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "[]";
            json = json.Trim();

            if (json.EndsWith("]")) return json;

            int lastCloseBrace = json.LastIndexOf('}');
            if (lastCloseBrace > 0)
            {
                return json.Substring(0, lastCloseBrace + 1) + "\n]";
            }

            return json.EndsWith("]") ? json : json + "]";
        }

        private static string ExtractLang(JsonElement elem, params string[] patterns)
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

        private static List<ExtractedBlock> SegmentTextToBlocks(string rawText)
        {
            var result = new List<ExtractedBlock>();
            if (string.IsNullOrWhiteSpace(rawText)) return result;

            rawText = SanitizeOcrArtifacts(rawText);
            rawText = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

            var rawParagraphs = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            bool isFirstParagraphInChapter = true;

            foreach (var para in rawParagraphs)
            {
                if (string.IsNullOrWhiteSpace(para)) continue;

                var sentences = Regex.Split(para, @"(?<=[\.!\?。！？][”""」』]?)\s*(?=[A-Z0-9“""ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯĂẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼỀỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴÝỶỸ–-]|[\u4e00-\u9fa5\u3040-\u30ff])")
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                bool isFirstInParagraph = true;

                foreach (var sentence in sentences)
                {
                    string cleanSentence = sentence;
                    int blockType = 1;

                    bool startsWithDash = cleanSentence.StartsWith("-") || cleanSentence.StartsWith("–");

                    if (startsWithDash)
                    {
                        blockType = 4;
                        cleanSentence = Regex.Replace(cleanSentence, @"^[–-]\s*", "").Trim();
                    }
                    else if (isFirstParagraphInChapter)
                    {
                        blockType = 0;
                        isFirstParagraphInChapter = false;
                    }
                    else if (Regex.IsMatch(cleanSentence, @"[""“][^""”]+[""”]"))
                    {
                        blockType = 4;
                    }
                    else if (isFirstInParagraph)
                    {
                        blockType = 2;
                    }

                    isFirstInParagraph = false;

                    if (!string.IsNullOrWhiteSpace(cleanSentence))
                    {
                        result.Add(new ExtractedBlock
                        {
                            BlockType = blockType,
                            English = cleanSentence
                        });
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
            text = text.Replace("’", "'").Replace("`", "'");
            text = Regex.Replace(text, @"\bmisdemean\s+ours\b", "misdemeanours", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bmain\s+tained\b", "maintained", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bsamo\s+vars\b", "samovars", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bex\s+erted\b", "exerted", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bpot-hellied\b", "pot-bellied", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bbuflfet\b", "buffet", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\blell\b", "tell", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"([.!?])\s+([”""])", "$1$2");
            text = Regex.Replace(text, @"([“""])\s+", "$1");

            return text;
        }

        private string ReadRawTextFromFile(string? filePath)
        {
            // Kiểm tra nếu đường dẫn rỗng hoặc tệp không tồn tại trên ổ đĩa -> Trả về chuỗi rỗng thay vì ném lỗi FileNotFoundException
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return string.Empty;
            }

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
                if (package.Workbook.Worksheets.Count > 0)
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
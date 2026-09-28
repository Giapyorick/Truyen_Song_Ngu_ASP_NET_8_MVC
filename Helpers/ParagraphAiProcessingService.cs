using System;
using System.Collections.Generic;
using System.Globalization;
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

                while (true)
                {
                    int savedCount = await _context.TblParagraphs
                        .CountAsync(p => p.ChapterId == chapterId);

                    if (savedCount >= allBlocks.Count)
                    {
                        _logger.LogInformation($"[Hangfire] Completed 100% ({allBlocks.Count} sentences) for Chapter ID: {chapterId}.");
                        break;
                    }

                    var currentBatch = allBlocks.Skip(savedCount).Take(chunkSize).ToList();
                    if (currentBatch.Count == 0) break;

                    int currentMaxOrder = await _context.TblParagraphs
                        .Where(p => p.ChapterId == chapterId)
                        .Select(p => (int?)p.ParagraphOrder)
                        .MaxAsync() ?? -1;

                    int orderTracker = currentMaxOrder + 1;

                    if (savedCount == 0 && currentBatch.Count > 0)
                    {
                        currentBatch[0].BlockType = 0;
                    }

                    _logger.LogInformation($"[Hangfire] Translating batch: #{savedCount + 1} to #{savedCount + currentBatch.Count} / {allBlocks.Count}");

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

            var rawParagraphs = Regex.Split(rawText, @"\n\s*\n+")
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            foreach (var para in rawParagraphs)
            {
                string cleanPara = Regex.Replace(para, @"(\b\w+)-?\n+(\w+\b)", "$1$2");
                cleanPara = Regex.Replace(cleanPara, @"\s*\n+\s*", " ").Trim();
                cleanPara = Regex.Replace(cleanPara, @"[ ]{2,}", " ");

                if (string.IsNullOrWhiteSpace(cleanPara)) continue;

                var dialogueRegex = new Regex(@"([“""][^”""]+?[”""])", RegexOptions.Compiled);
                var parts = dialogueRegex.Split(cleanPara)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                bool isFirstPartInParagraph = true;

                foreach (var part in parts)
                {
                    bool isDialogue = (part.StartsWith("\"") && part.EndsWith("\"")) ||
                                      (part.StartsWith("“") && part.EndsWith("”"));

                    if (isDialogue)
                    {
                        string formattedQuote = "\"" + part.Substring(1, part.Length - 2).Trim() + "\"";
                        result.Add(new ExtractedBlock
                        {
                            BlockType = 4,
                            English = formattedQuote
                        });
                        isFirstPartInParagraph = false;
                    }
                    else
                    {
                        var sentences = Regex.Split(part, @"(?<=[\.!\?][”""]?)\s+(?=[A-Z0-9“""])")
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();

                        foreach (var sentence in sentences)
                        {
                            int blockType;
                            if (isFirstPartInParagraph)
                            {
                                blockType = 2;
                                isFirstPartInParagraph = false;
                            }
                            else
                            {
                                blockType = 1;
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
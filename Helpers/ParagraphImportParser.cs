using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebTruyenTranh.Models;

namespace WebTruyenTranh.Services
{
    /// <summary>
    /// Đọc file import câu/đoạn (TXT, XLSX), tách câu, gán BlockType và ghi vào CSDL.
    /// Dùng chung cho: tblParagraphsController (import thường / phân tích) và ParagraphAiProcessingService (job AI).
    ///
    /// Định dạng file hỗ trợ:
    ///  1) .txt                         – văn bản thường, mỗi dòng là một đoạn, tự tách câu.
    ///  2) .xlsx 1 cột                  – cột A của sheet đầu, mỗi ô là một đoạn, tự tách câu.
    ///  3) .xlsx NHIỀU CỘT (có tiêu đề) – một dòng tiêu đề chứa tên ngôn ngữ (English / Vietnamese / Chinese /
    ///     Japanese / French, hoặc Tiếng Anh / Tiếng Việt..., hoặc EN / VI...), có thể kèm cột BlockType.
    ///     Mỗi dòng bên dưới là MỘT câu đã căn sẵn giữa các ngôn ngữ (không tách câu lại).
    ///     File "Xuất Excel" của trang quản lý câu/đoạn import lại được nguyên trạng.
    ///  .xls (Excel 97-2003) KHÔNG hỗ trợ – cần lưu lại thành .xlsx.
    /// </summary>
    public static class ParagraphImportParser
    {
        public const string English = "english";
        public const string Vietnamese = "vietnamese";
        public const string Chinese = "chinese";
        public const string Japanese = "japanese";
        public const string French = "french";

        /// <summary>Các ngôn ngữ đích (ngoài tiếng Anh nguồn)</summary>
        public static readonly string[] TargetLanguages = { Vietnamese, Chinese, Japanese, French };

        // ------------------------------------------------------------------
        // MÔ HÌNH DỮ LIỆU
        // ------------------------------------------------------------------
        public class Block
        {
            public int BlockType { get; set; } = 1;
            public string Text { get; set; } = "";
        }

        /// <summary>Nội dung một file đã đọc</summary>
        public class SourceFile
        {
            public string FileName { get; set; } = "";
            /// <summary>true = Excel nhiều cột có dòng tiêu đề ngôn ngữ</summary>
            public bool IsTable { get; set; }
            /// <summary>Văn bản thô (file TXT / Excel 1 cột)</summary>
            public string RawText { get; set; } = "";
            /// <summary>Các dòng của bảng (Excel nhiều cột): ngôn ngữ -> nội dung</summary>
            public List<Dictionary<string, string>> Rows { get; set; } = new();
            /// <summary>BlockType theo từng dòng của bảng (nếu có cột BlockType hợp lệ)</summary>
            public List<int?> RowBlockTypes { get; set; } = new();
            /// <summary>Ngôn ngữ có cột trong bảng (english, vietnamese...)</summary>
            public HashSet<string> TableLanguages { get; set; } = new();

            public bool IsEmpty => IsTable ? Rows.Count == 0 : string.IsNullOrWhiteSpace(RawText);
        }

        /// <summary>Lỗi đọc file có thông báo dễ hiểu cho người dùng</summary>
        public class ImportFileException : Exception
        {
            public ImportFileException(string message) : base(message) { }
        }

        /// <summary>
        /// Dữ liệu một lần import, lưu ra file JSON (ngoài wwwroot) để job Hangfire xử lý.
        /// </summary>
        public class ImportPayload
        {
            public int ChapterId { get; set; }
            /// <summary>Câu tiếng Anh nguồn (đã tách, có BlockType)</summary>
            public List<Block> English { get; set; } = new();
            /// <summary>Bản dịch đã khớp 1-1 với tiếng Anh (từ file hoặc từ cột Excel)</summary>
            public Dictionary<string, List<string>> Provided { get; set; } = new();
            /// <summary>Bản dịch lệch số câu – cần AI căn chỉnh lại</summary>
            public Dictionary<string, List<string>> ToAlign { get; set; } = new();
            /// <summary>Ngôn ngữ cần AI dịch từ tiếng Anh</summary>
            public List<string> AiLanguages { get; set; } = new();
            /// <summary>Model AI admin chọn (AiCompletionService.Models, ví dụ "claude:claude-sonnet-5-5"). Rỗng = mặc định</summary>
            public string AiModel { get; set; } = "";

            public bool NeedsAi => AiLanguages.Count > 0 || ToAlign.Count > 0;
        }

        // ------------------------------------------------------------------
        // ĐỌC FILE
        // ------------------------------------------------------------------
        public static SourceFile Read(Stream stream, string fileName)
        {
            string ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            var result = new SourceFile { FileName = Path.GetFileName(fileName ?? "") };

            if (ext == ".txt")
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                result.RawText = reader.ReadToEnd();
                return result;
            }

            if (ext == ".xls")
                throw new ImportFileException($"File \"{result.FileName}\" là định dạng Excel 97-2003 (.xls) không được hỗ trợ. Hãy mở bằng Excel và lưu lại thành .xlsx.");

            if (ext != ".xlsx")
                throw new ImportFileException($"File \"{result.FileName}\" không đúng định dạng. Chỉ hỗ trợ .xlsx và .txt.");

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            try
            {
                using var package = new ExcelPackage(stream);
                if (package.Workbook.Worksheets.Count == 0) return result;

                var sheet = package.Workbook.Worksheets[0];
                if (sheet.Dimension == null) return result;

                int lastRow = sheet.Dimension.End.Row;
                int lastCol = Math.Min(sheet.Dimension.End.Column, 50);

                // Tìm dòng tiêu đề trong 6 dòng đầu (file xuất Excel có tiêu đề ở dòng 3)
                int headerRow = 0;
                var columns = new Dictionary<int, string>();   // cột -> ngôn ngữ / "blocktype"
                for (int r = 1; r <= Math.Min(6, lastRow) && headerRow == 0; r++)
                {
                    var found = new Dictionary<int, string>();
                    for (int c = 1; c <= lastCol; c++)
                    {
                        var key = HeaderKey(sheet.Cells[r, c].Text);
                        if (key != null && !found.ContainsValue(key)) found[c] = key;
                    }
                    // Bảng nhiều cột: có ít nhất 2 cột ngôn ngữ (1 cột thì đọc như file 1 cột bình thường)
                    if (found.Values.Count(v => v != "blocktype") >= 2)
                    {
                        headerRow = r;
                        columns = found;
                    }
                }

                if (headerRow > 0)
                {
                    result.IsTable = true;
                    foreach (var lang in columns.Values.Where(v => v != "blocktype")) result.TableLanguages.Add(lang);

                    for (int r = headerRow + 1; r <= lastRow; r++)
                    {
                        var row = new Dictionary<string, string>();
                        int? bt = null;
                        foreach (var (col, key) in columns)
                        {
                            string text = sheet.Cells[r, col].Text?.Trim() ?? "";
                            if (key == "blocktype")
                            {
                                if (int.TryParse(text, out int v) && v >= 0 && v <= 4) bt = v;
                            }
                            else
                            {
                                row[key] = text;
                            }
                        }

                        // Bỏ dòng trống hoàn toàn
                        if (row.Values.All(string.IsNullOrWhiteSpace)) continue;
                        result.Rows.Add(row);
                        result.RowBlockTypes.Add(bt);
                    }
                    return result;
                }

                // Excel 1 cột: cột A, mỗi ô là một đoạn
                var sb = new StringBuilder();
                for (int r = 1; r <= lastRow; r++)
                {
                    string cell = sheet.Cells[r, 1].Text;
                    if (string.IsNullOrWhiteSpace(cell)) continue;
                    // Bỏ ô tiêu đề kiểu "English" / "Tiếng Việt" ở đầu cột (trước đây bị import thành 1 câu)
                    if (r <= 3 && HeaderKey(cell) != null) continue;
                    sb.AppendLine(cell.Trim());
                }
                result.RawText = sb.ToString();
                return result;
            }
            catch (ImportFileException) { throw; }
            catch (Exception ex)
            {
                throw new ImportFileException($"Không đọc được file Excel \"{result.FileName}\" (file hỏng hoặc không phải .xlsx thật): {ex.Message}");
            }
        }

        /// <summary>Nhận diện tiêu đề cột: trả về tên ngôn ngữ chuẩn hoặc "blocktype", null nếu không phải</summary>
        public static string? HeaderKey(string? header)
        {
            if (string.IsNullOrWhiteSpace(header)) return null;
            string h = header.Trim().ToLowerInvariant();
            h = Regex.Replace(h, @"[\s_\-\(\)\[\]🇺🇸🇻🇳🇨🇳🇯🇵🇫🇷]+", " ").Trim();

            return h switch
            {
                "english" or "tiếng anh" or "tieng anh" or "en" or "eng" or "anh" => English,
                "vietnamese" or "tiếng việt" or "tieng viet" or "vi" or "vie" or "vn" or "việt" => Vietnamese,
                "chinese" or "tiếng trung" or "tieng trung" or "zh" or "cn" or "zho" or "中文" or "trung" => Chinese,
                "japanese" or "tiếng nhật" or "tieng nhat" or "ja" or "jp" or "jpn" or "日本語" or "nhật" => Japanese,
                "french" or "tiếng pháp" or "tieng phap" or "fr" or "fra" or "français" or "francais" or "pháp" => French,
                "blocktype" or "block type" or "loại khối" or "loai khoi" or "kiểu đoạn" => "blocktype",
                _ => null
            };
        }

        /// <summary>Chuẩn hóa tên ngôn ngữ người dùng gửi lên (Vietnamese, vi, Tiếng Việt...)</summary>
        public static string? NormalizeLanguage(string? name)
        {
            var key = HeaderKey(name);
            return key == "blocktype" ? null : key;
        }

        // ------------------------------------------------------------------
        // TÁCH CÂU + BLOCKTYPE
        // ------------------------------------------------------------------
        // Chữ viết tắt kết thúc bằng dấu chấm nhưng KHÔNG phải hết câu (Mr. Smith, Dr. Watson, St. Paul...)
        private static readonly Regex AbbreviationRegex = new(
            @"\b(Mr|Mrs|Ms|Mx|Dr|Prof|Sr|Jr|St|Mt|Ft|Capt|Col|Gen|Lt|Sgt|Cpl|Maj|Adm|Rev|Hon|Gov|Sen|Rep|Pres|Messrs|Mme|Mlle|vs|etc|approx|dept|est|No|Vol|Ch|pp|fig|i\.e|e\.g|a\.m|p\.m)\.",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Chữ cái viết tắt tên người: "J. K. Rowling", "George R. R. Martin"
        private static readonly Regex InitialRegex = new(@"\b([A-Z])\.(?=\s*[A-Z][\.a-z])", RegexOptions.Compiled);

        private const char DotPlaceholder = '';

        private static readonly Regex SentenceSplitRegex = new(
            @"(?<=[\.!\?。！？…][”""’'」』\)]?)\s*(?=[A-Z0-9“""‘ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼỀẾỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴÝỶỸ–\-]|[一-龥぀-ヿ])",
            RegexOptions.Compiled);

        /// <summary>
        /// Tách văn bản thành câu, gán BlockType: 0 = mở đầu chương, 2 = đầu đoạn, 1 = câu tiếp theo, 4 = hội thoại.
        /// </summary>
        public static List<Block> Segment(string rawText, bool sanitizeOcr = false)
        {
            var result = new List<Block>();
            if (string.IsNullOrWhiteSpace(rawText)) return result;

            if (sanitizeOcr) rawText = SanitizeOcrArtifacts(rawText);
            rawText = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

            var paragraphs = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool isFirstOfChapter = true;

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para)) continue;

                // Che dấu chấm của chữ viết tắt để không bị tách sai, tách xong thì trả lại
                string protectedPara = AbbreviationRegex.Replace(para, m => m.Value.Replace('.', DotPlaceholder));
                protectedPara = InitialRegex.Replace(protectedPara, m => m.Groups[1].Value + DotPlaceholder);

                var sentences = SentenceSplitRegex.Split(protectedPara)
                    .Select(s => s.Replace(DotPlaceholder, '.').Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                bool isFirstInParagraph = true;
                foreach (var sentence in sentences)
                {
                    var (blockType, text) = DetectBlockType(sentence, isFirstOfChapter, isFirstInParagraph);
                    if (blockType == 0) isFirstOfChapter = false;
                    else if (isFirstOfChapter && blockType == 4) isFirstOfChapter = false;
                    isFirstInParagraph = false;

                    if (!string.IsNullOrWhiteSpace(text))
                        result.Add(new Block { BlockType = blockType, Text = text });
                }
            }
            return result;
        }

        /// <summary>BlockType cho một câu (dùng cả khi tách câu và khi đọc bảng không có cột BlockType)</summary>
        public static (int BlockType, string Text) DetectBlockType(string sentence, bool isFirstOfChapter, bool isFirstInParagraph)
        {
            string text = sentence.Trim();
            if (text.StartsWith("-") || text.StartsWith("–") || text.StartsWith("—"))
                return (4, Regex.Replace(text, @"^[–—-]\s*", "").Trim());
            if (isFirstOfChapter) return (0, text);
            if (Regex.IsMatch(text, @"[""“][^""”]+[""”]")) return (4, text);
            if (isFirstInParagraph) return (2, text);
            return (1, text);
        }

        /// <summary>Lấy câu tiếng Anh nguồn từ file đã đọc</summary>
        public static List<Block> EnglishBlocks(SourceFile file, bool sanitizeOcr = false)
        {
            if (!file.IsTable) return Segment(file.RawText, sanitizeOcr);

            if (!file.TableLanguages.Contains(English))
                throw new ImportFileException($"File \"{file.FileName}\" có nhiều cột nhưng không có cột English / Tiếng Anh.");

            var blocks = new List<Block>();
            bool first = true;
            for (int i = 0; i < file.Rows.Count; i++)
            {
                string en = file.Rows[i].GetValueOrDefault(English, "");
                if (string.IsNullOrWhiteSpace(en)) continue;

                int? bt = file.RowBlockTypes[i];
                if (bt.HasValue)
                {
                    blocks.Add(new Block { BlockType = bt.Value, Text = CleanLeadingDash(en) });
                }
                else
                {
                    var (type, text) = DetectBlockType(en, first, true);
                    blocks.Add(new Block { BlockType = type == 2 ? 1 : type, Text = text });
                }
                first = false;
            }
            return blocks;
        }

        /// <summary>
        /// Lấy các dòng của 1 ngôn ngữ từ file đã đọc.
        /// Bảng: lấy theo cột ngôn ngữ, giữ nguyên số dòng có tiếng Anh (nếu bảng có cột English) để khớp 1-1.
        /// </summary>
        public static List<string> LanguageLines(SourceFile file, string lang)
        {
            if (!file.IsTable)
                return Segment(file.RawText).Select(b => b.Text).ToList();

            if (!file.TableLanguages.Contains(lang))
                throw new ImportFileException($"File \"{file.FileName}\" có nhiều cột nhưng không có cột {lang}.");

            bool hasEnglish = file.TableLanguages.Contains(English);
            return file.Rows
                .Where(r => !hasEnglish || !string.IsNullOrWhiteSpace(r.GetValueOrDefault(English, "")))
                .Select(r => r.GetValueOrDefault(lang, ""))
                .ToList();
        }

        public static string CleanLeadingDash(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return Regex.Replace(text.Trim(), @"^[–—-]\s*", "").Trim();
        }

        /// <summary>Sửa vài lỗi OCR hay gặp trong file nguồn tiếng Anh (dùng cho chế độ AI như trước)</summary>
        public static string SanitizeOcrArtifacts(string text)
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

        // ------------------------------------------------------------------
        // LƯU / ĐỌC PAYLOAD CHO JOB (thư mục App_Data – KHÔNG nằm trong wwwroot)
        // ------------------------------------------------------------------
        public static string PayloadFolder(string contentRootPath)
        {
            var folder = Path.Combine(contentRootPath, "App_Data", "paragraph_imports");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static async Task<string> SavePayloadAsync(ImportPayload payload, string contentRootPath)
        {
            var path = Path.Combine(PayloadFolder(contentRootPath), $"{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload), Encoding.UTF8);
            return path;
        }

        public static ImportPayload? LoadPayload(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            return JsonSerializer.Deserialize<ImportPayload>(File.ReadAllText(path, Encoding.UTF8));
        }

        // ------------------------------------------------------------------
        // GHI VÀO CSDL – một lần, trong transaction (lỗi thì không ghi gì)
        // ------------------------------------------------------------------
        public static async Task<int> SaveAsync(
            TruyenSongNguContext context,
            int chapterId,
            List<Block> english,
            IReadOnlyDictionary<string, List<string>> translations)
        {
            if (english.Count == 0) return 0;

            await using var transaction = await context.Database.BeginTransactionAsync();

            int currentMaxOrder = await context.TblParagraphs
                .Where(p => p.ChapterId == chapterId)
                .Select(p => (int?)p.ParagraphOrder)
                .MaxAsync() ?? -1;
            bool chapterIsEmpty = currentMaxOrder < 0;
            int order = currentMaxOrder + 1;

            string? Line(string lang, int i)
            {
                if (!translations.TryGetValue(lang, out var list) || list == null || i >= list.Count) return null;
                var t = CleanLeadingDash(list[i]);
                return string.IsNullOrWhiteSpace(t) ? null : t;
            }

            var entities = new List<TblParagraph>(english.Count);
            for (int i = 0; i < english.Count; i++)
            {
                int blockType = english[i].BlockType;
                // "Mở đầu chương" (0) chỉ dành cho câu đầu tiên của chương còn trống.
                // Nối thêm vào chương đã có nội dung -> câu đầu file là đầu đoạn (2).
                if (chapterIsEmpty && i == 0) blockType = 0;
                else if (blockType == 0) blockType = 2;

                entities.Add(new TblParagraph
                {
                    ChapterId = chapterId,
                    ParagraphOrder = order++,
                    BlockType = blockType,
                    English = english[i].Text.Trim(),
                    Vietnamese = Line(Vietnamese, i) ?? "",
                    Chinese = Line(Chinese, i),
                    Japanese = Line(Japanese, i),
                    French = Line(French, i)
                });
            }

            await context.TblParagraphs.AddRangeAsync(entities);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_ReindexParagraphOrder @ChapID = {chapterId}");
            await transaction.CommitAsync();
            return entities.Count;
        }
    }
}

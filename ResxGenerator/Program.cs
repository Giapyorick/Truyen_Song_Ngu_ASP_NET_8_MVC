using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace WebTruyenTranh.ResxGenerator
{
    public class ResxGenerator
    {
        // ============================================================
        // CONFIGURATION
        // ============================================================

        private const string ExcelFile = "C:/Users/HP/Downloads/Resources_TSN_Story_AI.xlsx";

	private const string ResourceDirectory =
   	 @"D:\ASP.NET\WebTruyenTranh\Resources";

	private const string VietnameseResource =
    	"SharedResource.vi-VN.resx";

	private const string EnglishResource =
   	 "SharedResource.en-US.resx";

        // ============================================================
        // MAIN
        // ============================================================

        public static void Main(string[] args)
        {
            try
            {
                Console.OutputEncoding =
                    System.Text.Encoding.UTF8;

                Console.WriteLine("==========================================");
                Console.WriteLine("        RESX TRANSLATION GENERATOR");
                Console.WriteLine("==========================================");
                Console.WriteLine();

                string excelPath = ExcelFile;

                string resourceDirectory = ResourceDirectory;

                string viPath =
                    Path.Combine(
                        resourceDirectory,
                        VietnameseResource);

                string enPath =
                    Path.Combine(
                        resourceDirectory,
                        EnglishResource);

                // ----------------------------------------------------
                // CHECK EXCEL
                // ----------------------------------------------------

                if (!File.Exists(excelPath))
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;

                    Console.WriteLine(
                        $"Không tìm thấy file: {excelPath}");

                    Console.ResetColor();

                    return;
                }

                Directory.CreateDirectory(
                    resourceDirectory);

                // ----------------------------------------------------
                // READ EXCEL
                // ----------------------------------------------------

                Console.WriteLine(
                    $"Reading: {excelPath}");

                var translations =
                    ReadExcel(excelPath);

                Console.WriteLine(
                    $"Đã đọc {translations.Count} key.");

                Console.WriteLine();

                // ----------------------------------------------------
                // UPDATE VIETNAMESE RESX
                // ----------------------------------------------------

                Console.WriteLine(
                    "Updating Vietnamese resource...");

                int viAdded =
                    UpdateResx(
                        viPath,
                        translations,
                        "vi-VN");

                // ----------------------------------------------------
                // UPDATE ENGLISH RESX
                // ----------------------------------------------------

                Console.WriteLine(
                    "Updating English resource...");

                int enAdded =
                    UpdateResx(
                        enPath,
                        translations,
                        "en-US");

                // ----------------------------------------------------
                // RESULT
                // ----------------------------------------------------

                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Green;

                Console.WriteLine(
                    "==========================================");

                Console.WriteLine(
                    "              COMPLETED");

                Console.WriteLine(
                    "==========================================");

                Console.WriteLine(
                    $"Vietnamese added : {viAdded}");

                Console.WriteLine(
                    $"English added    : {enAdded}");

                Console.WriteLine();

                Console.WriteLine(
                    $"Vietnamese: {viPath}");

                Console.WriteLine(
                    $"English   : {enPath}");

                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;

                Console.WriteLine();
                Console.WriteLine("ERROR:");
                Console.WriteLine(ex.Message);

                Console.ResetColor();
            }
        }


        // ============================================================
        // READ EXCEL
        // ============================================================

        private static List<TranslationRow> ReadExcel(
            string filePath)
        {
            var result =
                new List<TranslationRow>();

            using var workbook =
                new XLWorkbook(filePath);

            var worksheet =
                workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
            {
                throw new Exception(
                    "Excel không có worksheet.");
            }

            // --------------------------------------------------------
            // FIND HEADER
            // --------------------------------------------------------

            var headerRow =
                worksheet.FirstRowUsed();

            if (headerRow == null)
            {
                throw new Exception(
                    "Excel không có dữ liệu.");
            }

            var headers =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var cell in headerRow.CellsUsed())
            {
                string value =
                    cell.GetString().Trim();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    headers[value] =
                        cell.Address.ColumnNumber;
                }
            }

            // --------------------------------------------------------
            // REQUIRED COLUMNS
            // --------------------------------------------------------

            if (!headers.ContainsKey("Name"))
            {
                throw new Exception(
                    "Excel phải có cột 'Name'.");
            }

            if (!headers.ContainsKey("vi-VN"))
            {
                throw new Exception(
                    "Excel phải có cột 'vi-VN'.");
            }

            if (!headers.ContainsKey("en-US"))
            {
                throw new Exception(
                    "Excel phải có cột 'en-US'.");
            }

            int nameColumn =
                headers["Name"];

            int viColumn =
                headers["vi-VN"];

            int enColumn =
                headers["en-US"];

            // --------------------------------------------------------
            // READ ROWS
            // --------------------------------------------------------

            var usedRows =
                worksheet.RowsUsed()
                    .Skip(1);

            var duplicateChecker =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            int rowNumber = 1;

            foreach (var row in usedRows)
            {
                rowNumber++;

                string name =
                    row.Cell(nameColumn)
                        .GetString()
                        .Trim();

                string vietnamese =
                    row.Cell(viColumn)
                        .GetString()
                        .Trim();

                string english =
                    row.Cell(enColumn)
                        .GetString()
                        .Trim();

                // ----------------------------------------------------
                // SKIP COMPLETELY EMPTY ROW
                // ----------------------------------------------------

                if (string.IsNullOrWhiteSpace(name) &&
                    string.IsNullOrWhiteSpace(vietnamese) &&
                    string.IsNullOrWhiteSpace(english))
                {
                    continue;
                }

                // ----------------------------------------------------
                // VALIDATE NAME
                // ----------------------------------------------------

                if (string.IsNullOrWhiteSpace(name))
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;

                    Console.WriteLine(
                        $"WARNING: Row {rowNumber} " +
                        "không có Name. Bỏ qua.");

                    Console.ResetColor();

                    continue;
                }

                // ----------------------------------------------------
                // CHECK DUPLICATE
                // ----------------------------------------------------

                if (!duplicateChecker.Add(name))
                {
                    throw new Exception(
                        $"Key '{name}' bị trùng " +
                        $"ở row {rowNumber}.");
                }

                // ----------------------------------------------------
                // ADD
                // ----------------------------------------------------

                result.Add(
                    new TranslationRow
                    {
                        Name = name,
                        Vietnamese = vietnamese,
                        English = english
                    });
            }

            return result;
        }


        // ============================================================
        // UPDATE RESX
        // ============================================================

        private static int UpdateResx(
            string filePath,
            List<TranslationRow> translations,
            string language)
        {
            XDocument document;

            // --------------------------------------------------------
            // LOAD OR CREATE RESX
            // --------------------------------------------------------

            if (File.Exists(filePath))
            {
                document =
                    XDocument.Load(filePath);
            }
            else
            {
                document =
                    CreateEmptyResx();
            }

            XElement root =
                document.Root
                ?? throw new Exception(
                    "RESX không có root element.");

            // --------------------------------------------------------
            // EXISTING KEYS
            // --------------------------------------------------------

            var existingData =
                root.Elements("data")
                    .Where(x =>
                        x.Attribute("name") != null)
                    .ToDictionary(
                        x =>
                            x.Attribute("name")!
                                .Value,
                        x => x,
                        StringComparer.OrdinalIgnoreCase);

            int added = 0;

            // --------------------------------------------------------
            // ADD / UPDATE
            // --------------------------------------------------------

            foreach (var item in translations)
            {
                string value =
                    language == "vi-VN"
                        ? item.Vietnamese
                        : item.English;

                // ----------------------------------------------------
                // EMPTY TRANSLATION
                // ----------------------------------------------------

                if (string.IsNullOrWhiteSpace(value))
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;

                    Console.WriteLine(
                        $"WARNING: {item.Name} " +
                        $"không có bản dịch {language}. " +
                        "Bỏ qua.");

                    Console.ResetColor();

                    continue;
                }

                // ----------------------------------------------------
                // ALREADY EXISTS
                // ----------------------------------------------------

                if (existingData.ContainsKey(item.Name))
                {
                    Console.ForegroundColor =
                        ConsoleColor.DarkGray;

                    Console.WriteLine(
                        $"SKIP: {item.Name}");

                    Console.ResetColor();

                    continue;
                }

                // ----------------------------------------------------
                // CREATE DATA ELEMENT
                // ----------------------------------------------------

                var data =
                    new XElement(
                        "data",
                        new XAttribute(
                            "name",
                            item.Name),

                        new XAttribute(
                            XNamespace.Xml +
                            "space",
                            "preserve"),

                        new XElement(
                            "value",
                            value)
                    );

                root.Add(data);

                added++;

                Console.ForegroundColor =
                    ConsoleColor.Green;

                Console.WriteLine(
                    $"ADD [{language}] " +
                    $"{item.Name} = {value}");

                Console.ResetColor();
            }

            // --------------------------------------------------------
            // SAVE
            // --------------------------------------------------------

            SaveResx(
                document,
                filePath);

            return added;
        }


        // ============================================================
        // CREATE EMPTY RESX
        // ============================================================

        private static XDocument CreateEmptyResx()
        {
            XNamespace xsd =
                "http://www.w3.org/2001/XMLSchema";

            XNamespace xsi =
                "http://www.w3.org/2001/XMLSchema-instance";

            return new XDocument(
                new XDeclaration(
                    "1.0",
                    "utf-8",
                    null),

                new XElement(
                    "root",

                    new XElement(
                        "resheader",
                        new XAttribute(
                            "name",
                            "resmimetype"),

                        new XElement(
                            "value",
                            "text/microsoft-resx")
                    ),

                    new XElement(
                        "resheader",
                        new XAttribute(
                            "name",
                            "version"),

                        new XElement(
                            "value",
                            "2.0")
                    ),

                    new XElement(
                        "resheader",
                        new XAttribute(
                            "name",
                            "reader"),

                        new XElement(
                            "value",
                            "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    ),

                    new XElement(
                        "resheader",
                        new XAttribute(
                            "name",
                            "writer"),

                        new XElement(
                            "value",
                            "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    )
                )
            );
        }


        // ============================================================
        // SAVE RESX
        // ============================================================

        private static void SaveResx(
            XDocument document,
            string filePath)
        {
            var settings =
                new System.Xml.XmlWriterSettings
                {
                    Indent = true,
                    Encoding =
                        new System.Text.UTF8Encoding(
                            false)
                };

            using var writer =
                System.Xml.XmlWriter.Create(
                    filePath,
                    settings);

            document.Save(writer);
        }


        // ============================================================
        // MODEL
        // ============================================================

        private class TranslationRow
        {
            public string Name { get; set; } = "";

            public string Vietnamese { get; set; } = "";

            public string English { get; set; } = "";
        }
    }
}
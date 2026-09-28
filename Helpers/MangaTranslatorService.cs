using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

public class MangaTranslatorService
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    private static readonly string ApiUrl = "http://localhost:5003/translate/image";
    public async Task<byte[]> TranslateImageAsync(IFormFile mangaFile)
    {
        if (mangaFile == null || mangaFile.Length == 0)
        {
            throw new ArgumentException("File ảnh không hợp lệ.");
        }

        using (var content = new MultipartFormDataContent())
        {
            using (var stream = mangaFile.OpenReadStream())
            {
                // 1. Cấu hình Content-Type rõ ràng cho StreamContent (Tránh Python parse nhầm thành Text)
                var streamContent = new StreamContent(stream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(mangaFile.ContentType ?? "image/png");

                // 2. Endpoint /translate/image của Python nhận tham số file có tên key là "file"
                content.Add(streamContent, "image", mangaFile.FileName);

                // 3. Các tham số Form bắt buộc của Manga Translator
                content.Add(new StringContent("VIE"), "target_lang");
                content.Add(new StringContent("groq"), "translator");
                content.Add(new StringContent("default"), "detector");

                // Gửi request
                HttpResponseMessage response = await _httpClient.PostAsync(ApiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }

                string errorDetail = await response.Content.ReadAsStringAsync();
                throw new Exception($"Lỗi từ Manga Translator API ({response.StatusCode}): {errorDetail}");
            }
        }
    }
}
namespace WebTruyenTranh.Helpers;
public interface IAiTranslationService
{
    Task<string> GetAiResponse(string prompt);
    Task<string> TranslateAsync(string text, string fromLang, string toLang);
}

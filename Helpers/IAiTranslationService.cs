namespace WebTruyenTranh.Helpers;
public interface IAiTranslationService
{
    Task<string> GetAiResponse(string prompt);
}

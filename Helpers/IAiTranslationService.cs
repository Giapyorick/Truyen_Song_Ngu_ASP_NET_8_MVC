using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebTruyenTranh.Helpers
{
    public interface IAiTranslationService
    {
        Task<string> GetAiResponse(string prompt);
        Task<string> TranslateAsync(string text, string fromLang, string toLang);
        IAsyncEnumerable<string> StreamAiResponseAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
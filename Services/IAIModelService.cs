using AlovaChat.Models;

namespace AlovaChat.Services;

public interface IAIModelService
{
    Task InitializeAsync();
    Task<AIResponse> GenerateResponseAsync(AIRequest request);
    bool IsModelLoaded { get; }
    string ModelStatus { get; }
}
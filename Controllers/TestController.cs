using AlovaChat.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AlovaChat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IAIModelService _aiModelService;
    private readonly ILogger<TestController> _logger;

    public TestController(IAIModelService aiModelService, ILogger<TestController> logger)
    {
        _aiModelService = aiModelService;
        _logger = logger;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            IsModelLoaded = _aiModelService.IsModelLoaded,
            ModelStatus = _aiModelService.ModelStatus,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("chat")]
    public async Task<IActionResult> TestChat([FromBody] TestChatRequest request)
    {
        try
        {
            var aiRequest = new AlovaChat.Models.AIRequest
            {
                Prompt = request.Message ?? "Hello, how are you?",
                SessionId = "test-session",
                MaxTokens = 50,
                Temperature = 0.7f
            };

            var response = await _aiModelService.GenerateResponseAsync(aiRequest);

            return Ok(new
            {
                Success = response.IsSuccess,
                Response = response.Content,
                Error = response.ErrorMessage,
                ProcessingTime = response.ProcessingTime.TotalMilliseconds,
                Metadata = response.Metadata
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test chat");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("env")]
    public IActionResult GetEnvironmentInfo()
    {
        var apiKey = Environment.GetEnvironmentVariable("HUGGINGFACE_API_KEY");

        return Ok(new
        {
            HasApiKey = !string.IsNullOrEmpty(apiKey),
            ApiKeyLength = apiKey?.Length ?? 0,
            ApiKeyPrefix = apiKey?.Substring(0, Math.Min(10, apiKey?.Length ?? 0)) ?? "",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }
}

public class TestChatRequest
{
    public string? Message { get; set; }
}
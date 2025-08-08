using AlovaChat.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AlovaChat.Services
{
    public class KaggleAIModelService : IAIModelService
    {
        private readonly ILogger<KaggleAIModelService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IChatSessionService _sessionService;

        private bool _isModelLoaded = false;
        private string _modelStatus = "Not Initialized";
        private string _kaggleApiUrl;
        private int _maxTokens;
        private double _temperature;
        private int _requestTimeoutSeconds;
        private int _maxRetries;

        public bool IsModelLoaded => _isModelLoaded;
        public string ModelStatus => _modelStatus;

        public KaggleAIModelService(ILogger<KaggleAIModelService> logger, IConfiguration configuration, IChatSessionService sessionService)
        {
            _logger = logger;
            _configuration = configuration;
            _sessionService = sessionService;

            // Load configuration
            _kaggleApiUrl = _configuration["AIModel:KaggleApiUrl"] ?? "";
            _maxTokens = _configuration.GetValue<int>("AIModel:MaxTokens", 150);
            _temperature = _configuration.GetValue<double>("AIModel:Temperature", 0.7);
            _requestTimeoutSeconds = _configuration.GetValue<int>("AIModel:RequestTimeoutSeconds", 60);
            _maxRetries = _configuration.GetValue<int>("AIModel:MaxRetries", 3);

            // Configure HttpClient
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_requestTimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AlovaChat/1.0");
        }

        public async Task InitializeAsync()
        {
            try
            {
                _modelStatus = "Initializing Kaggle API connection...";
                _logger.LogInformation("Starting Kaggle AI model initialization");

                // Validate API URL
                if (string.IsNullOrEmpty(_kaggleApiUrl))
                {
                    _modelStatus = "Error: Missing Kaggle API URL";
                    _logger.LogError("KaggleApiUrl is not configured in appsettings.json");
                    _isModelLoaded = false;
                    return;
                }

                // Test API connection
                _modelStatus = "Testing Kaggle API connection...";
                var isAvailable = await TestKaggleApiAsync();

                if (isAvailable)
                {
                    _isModelLoaded = true;
                    _modelStatus = "Ready (Kaggle API)";
                    _logger.LogInformation("Kaggle AI service initialized successfully");
                }
                else
                {
                    _modelStatus = "Error: Kaggle API connection failed";
                    _logger.LogError("Failed to connect to Kaggle API");
                    _isModelLoaded = false;
                }
            }
            catch (Exception ex)
            {
                _modelStatus = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to initialize Kaggle AI service");
                _isModelLoaded = false;
            }
        }

        public async Task<AIResponse> GenerateResponseAsync(AIRequest request)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (!_isModelLoaded)
                {
                    stopwatch.Stop();
                    return new AIResponse
                    {
                        Content = "AI model is not available. Please check the Kaggle API connection.",
                        IsSuccess = false,
                        ErrorMessage = _modelStatus,
                        ProcessingTime = stopwatch.Elapsed
                    };
                }

                _logger.LogInformation("Sending request to Kaggle API");

                var result = await CallKaggleApiWithRetriesAsync(request);

                stopwatch.Stop();

                if (result.IsSuccess)
                {
                    return new AIResponse
                    {
                        Content = result.Response,
                        IsSuccess = true,
                        ProcessingTime = stopwatch.Elapsed
                    };
                }
                else
                {
                    return new AIResponse
                    {
                        Content = "I apologize, but I'm experiencing technical difficulties. Please try again.",
                        IsSuccess = false,
                        ErrorMessage = result.ErrorMessage,
                        ProcessingTime = stopwatch.Elapsed
                    };
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error generating AI response via Kaggle API");

                return new AIResponse
                {
                    Content = "I apologize, but I encountered an error while processing your request.",
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }

        private async Task<bool> TestKaggleApiAsync()
        {
            try
            {
                var testRequest = new AIRequest
                {
                    Prompt = "Hello",
                    SessionId = "test"
                };

                var result = await CallKaggleApiAsync(testRequest);
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kaggle API availability test failed");
                return false;
            }
        }

        private async Task<(bool IsSuccess, string Response, string? ErrorMessage)> CallKaggleApiWithRetriesAsync(AIRequest request)
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    var result = await CallKaggleApiAsync(request);

                    if (result.IsSuccess)
                    {
                        return result;
                    }

                    // If it's a temporary error, wait and retry
                    if (attempt < _maxRetries)
                    {
                        var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                        _logger.LogWarning("Kaggle API call failed (attempt {Attempt}/{MaxRetries}), retrying in {WaitTime}s: {Error}",
                            attempt, _maxRetries, waitTime.TotalSeconds, result.ErrorMessage);
                        await Task.Delay(waitTime);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Kaggle API call exception (attempt {Attempt}/{MaxRetries})", attempt, _maxRetries);

                    if (attempt >= _maxRetries)
                    {
                        return (false, "", ex.Message);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(attempt)); // Simple backoff
                }
            }

            return (false, "", "Max retries exceeded");
        }

        private async Task<(bool IsSuccess, string Response, string? ErrorMessage)> CallKaggleApiAsync(AIRequest request)
        {
            try
            {
                // Get conversation history from session service
                var conversationHistory = new List<object>();
                if (!string.IsNullOrEmpty(request.SessionId))
                {
                    var messages = await _sessionService.GetSessionMessagesAsync(request.SessionId);
                    conversationHistory = messages.TakeLast(5).Select(msg => new
                    {
                        role = msg.IsFromUser ? "user" : "assistant",
                        content = msg.Content
                    }).Cast<object>().ToList();
                }

                // Create the request payload for Kaggle API
                var kaggleRequest = new
                {
                    prompt = request.Prompt,
                    max_tokens = request.MaxTokens > 0 ? request.MaxTokens : _maxTokens,
                    temperature = request.Temperature > 0 ? request.Temperature : _temperature,
                    conversation_history = conversationHistory.ToArray()
                };

                var jsonRequest = JsonSerializer.Serialize(kaggleRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                _logger.LogInformation("Making request to Kaggle API: {ApiUrl}", _kaggleApiUrl);
                _logger.LogInformation("Request payload: {JsonRequest}", jsonRequest);

                var response = await _httpClient.PostAsync(_kaggleApiUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Kaggle API Response Status: {StatusCode}, Content: {Content}",
                    response.StatusCode, responseContent.Substring(0, Math.Min(500, responseContent.Length)));

                if (response.IsSuccessStatusCode)
                {
                    // Parse the response
                    var kaggleResponse = JsonSerializer.Deserialize<KaggleApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    if (kaggleResponse?.Response != null)
                    {
                        return (true, kaggleResponse.Response, null);
                    }
                    else
                    {
                        return (false, "", "Invalid response format from Kaggle API");
                    }
                }
                else
                {
                    return (false, "", $"Kaggle API error: {response.StatusCode} - {responseContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception calling Kaggle API");
                return (false, "", ex.Message);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // Response model for Kaggle API
    public class KaggleApiResponse
    {
        public string? Response { get; set; }
        public string? Error { get; set; }
        public bool Success { get; set; }
    }
}
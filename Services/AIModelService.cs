using AlovaChat.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AlovaChat.Services;

public class AIModelService : IAIModelService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIModelService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private readonly string _modelName;
    private readonly string _systemPrompt;
    private readonly int _requestTimeoutSeconds;
    private readonly int _maxRetries;
    private readonly string[] _availableModels;
    private readonly string _apiKey;
    private readonly bool _skipInitialTest;

    private bool _isModelLoaded = false;
    private string _modelStatus = "Not Initialized";

    public bool IsModelLoaded => _isModelLoaded;
    public string ModelStatus => _modelStatus;

    public AIModelService(IConfiguration configuration, ILogger<AIModelService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Load configuration from HuggingFace section
        _apiBaseUrl = "https://api-inference.huggingface.co/models";
        _modelName = _configuration["HuggingFace:ModelId"] ?? "HuggingFaceH4/zephyr-7b-beta";
        _systemPrompt = "You are AlovaChat, a helpful and knowledgeable AI assistant. Provide clear, accurate, and engaging responses to user questions and conversations.";
        _requestTimeoutSeconds = 60;
        _maxRetries = 3;
        _skipInitialTest = false;

        // Available models that work with HuggingFace Inference API
        _availableModels = new[] {
            "HuggingFaceH4/zephyr-7b-beta",
            "microsoft/DialoGPT-medium",
            "microsoft/DialoGPT-large",
            "facebook/blenderbot-400M-distill",
            "microsoft/DialoGPT-small"
        };

        // Get API key from configuration
        _apiKey = _configuration["HuggingFace:ApiKey"] ?? "";



        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("HuggingFace API Key not found in appsettings.json");
        }
        else
        {
            _logger.LogInformation("HuggingFace API Key loaded successfully (length: {Length})", _apiKey.Length);
        }

        // Configure HttpClient
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(_requestTimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AlovaChat/1.0");

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            _modelStatus = "Initializing Hugging Face API connection...";
            _logger.LogInformation("Starting AI model initialization with Hugging Face API");
            _logger.LogInformation("Model: {ModelName}, API Base: {ApiBaseUrl}", _modelName, _apiBaseUrl);

            // Validate API key
            if (string.IsNullOrEmpty(_apiKey))
            {
                _modelStatus = "Error: Missing Hugging Face API key";
                _logger.LogError("HuggingFace:ApiKey is not set in appsettings.json");
                _isModelLoaded = false;
                return;
            }

            // Test API connection
            _modelStatus = "Testing API connection...";
            var connectionTest = await TestApiConnectionAsync();

            if (!connectionTest)
            {
                _modelStatus = "Error: API connection failed";
                _logger.LogError("Failed to connect to Hugging Face API");
                _isModelLoaded = false;
                return;
            }

            // Test model availability (optional)
            if (_skipInitialTest)
            {
                _isModelLoaded = true;
                _modelStatus = $"Ready (Hugging Face API - {_modelName}) - Test Skipped";
                _logger.LogInformation("AI model service initialized successfully with Hugging Face API model: {ModelName} (test skipped)", _modelName);

            }
            else
            {
                _modelStatus = "Testing model availability...";
                var modelTest = await TestModelAvailabilityAsync();

                if (modelTest)
                {
                    _isModelLoaded = true;
                    _modelStatus = $"Ready (Hugging Face API - {_modelName})";
                    _logger.LogInformation("AI model service initialized successfully with Hugging Face API model: {ModelName}", _modelName);
                }
                else
                {
                    // Even if test fails, allow the service to work (model might be cold starting)
                    _isModelLoaded = true;
                    _modelStatus = $"Ready (Hugging Face API - {_modelName}) - Test Failed but Service Active";
                    _logger.LogWarning("Model availability test failed for {ModelName}, but service will remain active", _modelName);
                }
            }
        }
        catch (Exception ex)
        {
            _modelStatus = $"Error: {ex.Message}";
            _logger.LogError(ex, "Failed to initialize AI model service");
            _isModelLoaded = false;
        }
    }

    private async Task<bool> TestApiConnectionAsync()
    {
        try
        {
            // Test with a simple model info request
            var response = await _httpClient.GetAsync($"https://huggingface.co/api/models/{_modelName}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API connection test failed");
            return false;
        }
    }

    private async Task<bool> TestModelAvailabilityAsync()
    {
        try
        {
            var testRequest = new HuggingFaceRequest
            {
                Inputs = "Hello, this is a test.",
                Parameters = new HuggingFaceParameters
                {
                    MaxNewTokens = 10,
                    Temperature = 0.1f,
                    DoSample = true,
                    ReturnFullText = false
                },
                Options = new HuggingFaceOptions
                {
                    WaitForModel = true,
                    UseCache = false
                }
            };

            var result = await CallHuggingFaceApiAsync(testRequest);
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Model availability test failed");
            return false;
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
                    Content = "AI model is not available. Please check the model status and API key.",
                    IsSuccess = false,
                    ErrorMessage = _modelStatus,
                    ProcessingTime = stopwatch.Elapsed
                };
            }

            // Build the prompt
            var prompt = BuildPrompt(request);

            _logger.LogInformation("Sending request to Hugging Face API - Model: {ModelName}, Prompt: {Prompt}",
                _modelName, prompt.Substring(0, Math.Min(100, prompt.Length)) + "...");

            // Create Hugging Face request with configuration values
            var hfRequest = new HuggingFaceRequest
            {
                Inputs = prompt,
                Parameters = new HuggingFaceParameters
                {
                    MaxNewTokens = request.MaxTokens > 0 ? request.MaxTokens : _configuration.GetValue<int>("HuggingFace:MaxTokens", 2048),
                    Temperature = request.Temperature > 0 ? request.Temperature : _configuration.GetValue<float>("HuggingFace:Temperature", 0.7f),
                    TopP = _configuration.GetValue<float>("HuggingFace:TopP", 0.9f),
                    RepetitionPenalty = 1.1f,
                    DoSample = true,
                    ReturnFullText = false
                },
                Options = new HuggingFaceOptions
                {
                    WaitForModel = true,
                    UseCache = _configuration.GetValue<bool>("HuggingFace:EnableResponseCaching", true)
                }
            };

            // Call API with retries
            var result = await CallHuggingFaceApiWithRetriesAsync(hfRequest);

            stopwatch.Stop();

            _logger.LogInformation("Hugging Face API response - Success: {IsSuccess}, Response: {Response}, Error: {ErrorMessage}",
                result.IsSuccess, result.Response?.Substring(0, Math.Min(200, result.Response?.Length ?? 0)) + "...", result.ErrorMessage);

            if (result.IsSuccess)
            {
                var cleanedResponse = CleanResponse(result.Response, prompt);

                return new AIResponse
                {
                    Content = cleanedResponse,
                    IsSuccess = true,
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["model"] = _modelName,
                        ["provider"] = "HuggingFace API",
                        ["tokens_generated"] = EstimateTokenCount(cleanedResponse),
                        ["session_id"] = request.SessionId,
                        ["temperature"] = request.Temperature,
                        ["max_tokens"] = request.MaxTokens,
                        ["api_response_time"] = result.ResponseTime?.TotalMilliseconds ?? 0
                    }
                };
            }
            else
            {
                _logger.LogError("Hugging Face API error: {Error}", result.ErrorMessage);

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
            _logger.LogError(ex, "Error generating AI response for session {SessionId}", request.SessionId);
            stopwatch.Stop();

            return new AIResponse
            {
                Content = "I apologize, but I'm experiencing technical difficulties. Please try again.",
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<(bool IsSuccess, string Response, string? ErrorMessage, TimeSpan? ResponseTime)> CallHuggingFaceApiWithRetriesAsync(HuggingFaceRequest request)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var result = await CallHuggingFaceApiAsync(request);

                if (result.IsSuccess)
                {
                    return result;
                }

                // If model is loading, wait and retry
                if (result.ErrorMessage?.Contains("loading") == true && attempt < _maxRetries)
                {
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                    _logger.LogInformation("Model is loading, waiting {WaitTime}s before retry {Attempt}/{MaxRetries}",
                        waitTime.TotalSeconds, attempt + 1, _maxRetries);
                    await Task.Delay(waitTime);
                    continue;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API call attempt {Attempt}/{MaxRetries} failed", attempt, _maxRetries);

                if (attempt == _maxRetries)
                {
                    return (false, "", ex.Message, null);
                }

                await Task.Delay(TimeSpan.FromSeconds(attempt)); // Simple backoff
            }
        }

        return (false, "", "Max retries exceeded", null);
    }

    private async Task<(bool IsSuccess, string Response, string? ErrorMessage, TimeSpan? ResponseTime)> CallHuggingFaceApiAsync(HuggingFaceRequest request)
    {
        var requestStopwatch = Stopwatch.StartNew();

        try
        {
            var jsonRequest = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var apiUrl = $"{_apiBaseUrl}/{_modelName}";

            _logger.LogInformation("Making API request to: {ApiUrl}", apiUrl);
            _logger.LogInformation("Request payload: {JsonRequest}", jsonRequest);
            _logger.LogInformation("Authorization header present: {HasAuth}",
                _httpClient.DefaultRequestHeaders.Authorization != null);

            var response = await _httpClient.PostAsync(apiUrl, content);
            requestStopwatch.Stop();

            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("API Response Status: {StatusCode}, Content: {Content}",
                response.StatusCode, responseContent.Substring(0, Math.Min(500, responseContent.Length)));

            if (response.IsSuccessStatusCode)
            {
                // Handle different response formats
                if (responseContent.StartsWith("["))
                {
                    // Array response (most common)
                    var responses = JsonSerializer.Deserialize<HuggingFaceResponse[]>(responseContent);
                    var firstResponse = responses?.FirstOrDefault();

                    if (firstResponse?.GeneratedText != null)
                    {
                        return (true, firstResponse.GeneratedText, null, requestStopwatch.Elapsed);
                    }
                    else if (firstResponse?.Conversation?.GeneratedResponses?.Length > 0)
                    {
                        return (true, firstResponse.Conversation.GeneratedResponses[0], null, requestStopwatch.Elapsed);
                    }
                }
                else
                {
                    // Single object response
                    var singleResponse = JsonSerializer.Deserialize<HuggingFaceResponse>(responseContent);

                    if (singleResponse?.GeneratedText != null)
                    {
                        return (true, singleResponse.GeneratedText, null, requestStopwatch.Elapsed);
                    }
                    else if (singleResponse?.Conversation?.GeneratedResponses?.Length > 0)
                    {
                        return (true, singleResponse.Conversation.GeneratedResponses[0], null, requestStopwatch.Elapsed);
                    }
                }

                return (false, "", "No valid response content", requestStopwatch.Elapsed);
            }
            else
            {
                // Handle error response
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<HuggingFaceErrorResponse>(responseContent);
                    var errorMessage = errorResponse?.Error ?? $"API error: {response.StatusCode}";

                    if (errorResponse?.EstimatedTime > 0)
                    {
                        errorMessage += $" (Model loading, estimated time: {errorResponse.EstimatedTime}s)";
                    }

                    return (false, "", errorMessage, requestStopwatch.Elapsed);
                }
                catch
                {
                    return (false, "", $"API error: {response.StatusCode} - {responseContent}", requestStopwatch.Elapsed);
                }
            }
        }
        catch (Exception ex)
        {
            requestStopwatch.Stop();
            return (false, "", ex.Message, requestStopwatch.Elapsed);
        }
    }

    private string BuildPrompt(AIRequest request)
    {
        var promptBuilder = new StringBuilder();

        // For Mistral models, use the proper instruction format
        if (_modelName.Contains("mistralai/Mistral") || _modelName.Contains("Mistral"))
        {
            promptBuilder.Append("<s>[INST] ");

            if (!string.IsNullOrWhiteSpace(_systemPrompt))
            {
                promptBuilder.Append(_systemPrompt);
                promptBuilder.Append(" ");
            }

            if (!string.IsNullOrWhiteSpace(request.Context))
            {
                promptBuilder.Append($"Context: {request.Context} ");
            }

            promptBuilder.Append(request.Prompt);
            promptBuilder.Append(" [/INST]");
        }
        // For Zephyr models, use ChatML format
        else if (_modelName.Contains("zephyr") || _modelName.Contains("neural-chat"))
        {
            promptBuilder.AppendLine("<|system|>");
            if (!string.IsNullOrWhiteSpace(_systemPrompt))
            {
                promptBuilder.AppendLine(_systemPrompt);
            }
            promptBuilder.AppendLine("<|user|>");
            if (!string.IsNullOrWhiteSpace(request.Context))
            {
                promptBuilder.AppendLine($"Context: {request.Context}");
            }
            promptBuilder.AppendLine(request.Prompt);
            promptBuilder.Append("<|assistant|>");
        }
        // For OpenChat models
        else if (_modelName.Contains("openchat"))
        {
            promptBuilder.Append("GPT4 Correct User: ");
            if (!string.IsNullOrWhiteSpace(request.Context))
            {
                promptBuilder.Append($"Context: {request.Context} ");
            }
            promptBuilder.Append(request.Prompt);
            promptBuilder.Append("<|end_of_turn|>GPT4 Correct Assistant:");
        }
        // For conversational models like DialoGPT, use a simpler format
        else if (_modelName.Contains("DialoGPT") || _modelName.Contains("blenderbot"))
        {
            // Add context if available
            if (!string.IsNullOrWhiteSpace(request.Context))
            {
                promptBuilder.AppendLine(request.Context);
            }

            // Add the user's message
            promptBuilder.Append(request.Prompt);
        }
        else
        {
            // For general language models, use a more structured format
            if (!string.IsNullOrWhiteSpace(_systemPrompt))
            {
                promptBuilder.AppendLine(_systemPrompt);
                promptBuilder.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(request.Context))
            {
                promptBuilder.AppendLine($"Context: {request.Context}");
                promptBuilder.AppendLine();
            }

            promptBuilder.AppendLine($"Human: {request.Prompt}");
            promptBuilder.Append("Assistant:");
        }

        return promptBuilder.ToString();
    }

    private string CleanResponse(string response, string originalPrompt)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "I understand your message. How can I help you further?";
        }

        // Remove the original prompt if it's included in the response
        if (response.StartsWith(originalPrompt))
        {
            response = response.Substring(originalPrompt.Length).Trim();
        }

        // Clean up common artifacts
        response = response.Trim();

        // Remove repetitive patterns
        var lines = response.Split('\n');
        var cleanedLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrEmpty(trimmedLine) &&
                !cleanedLines.TakeLast(2).Contains(trimmedLine)) // Avoid immediate repetition
            {
                cleanedLines.Add(trimmedLine);
            }
        }

        response = string.Join("\n", cleanedLines);

        // Limit response length for safety
        if (response.Length > 800)
        {
            response = response.Substring(0, 800).TrimEnd();
            var lastSpace = response.LastIndexOf(' ');
            if (lastSpace > 600)
            {
                response = response.Substring(0, lastSpace) + "...";
            }
        }

        return string.IsNullOrWhiteSpace(response)
            ? "I understand your message. How can I help you further?"
            : response;
    }

    private static int EstimateTokenCount(string text)
    {
        // Rough estimation: 1 token ≈ 4 characters for English text
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
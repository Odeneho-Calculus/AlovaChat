using AlovaChat.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace AlovaChat.Services;

public class WikipediaSearchAIService : IAIModelService, IDisposable
{
    private readonly ILogger<WikipediaSearchAIService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    private bool _isModelLoaded = true;
    private string _modelStatus = "Ready - Wikipedia Search Engine";

    public bool IsModelLoaded => _isModelLoaded;
    public string ModelStatus => _modelStatus;

    public WikipediaSearchAIService(ILogger<WikipediaSearchAIService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "AlovaChat/1.0 (https://github.com/alovachat; contact@alovachat.com)");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _modelStatus = "Initializing Wikipedia Search Engine...";
            _logger.LogInformation("Starting Wikipedia Search AI service initialization");

            // Test Wikipedia API connectivity
            var testResponse = await _httpClient.GetAsync("https://en.wikipedia.org/api/rest_v1/page/summary/Wikipedia");
            if (testResponse.IsSuccessStatusCode)
            {
                _isModelLoaded = true;
                _modelStatus = "Ready - Wikipedia Search Engine";
                _logger.LogInformation("Wikipedia Search AI service initialized successfully");
            }
            else
            {
                _isModelLoaded = false;
                _modelStatus = "Error: Cannot connect to Wikipedia API";
                _logger.LogError("Failed to connect to Wikipedia API");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Wikipedia Search AI service");
            _isModelLoaded = false;
            _modelStatus = $"Error: {ex.Message}";
        }
    }

    public async Task<AIResponse> GenerateResponseAsync(AIRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing Wikipedia search query: {Query}", request.Prompt);

            // Search Wikipedia using OpenSearch API
            var searchResults = await SearchWikipediaAsync(request.Prompt);

            if (searchResults.Count == 0)
            {
                // Try alternative search strategies
                var alternativeResults = await TryAlternativeSearchAsync(request.Prompt);

                if (alternativeResults.Count > 0)
                {
                    var alternativeResponse = FormatWikipediaResponse(request.Prompt, alternativeResults);
                    stopwatch.Stop();

                    return new AIResponse
                    {
                        Content = alternativeResponse,
                        IsSuccess = true,
                        ProcessingTime = stopwatch.Elapsed,
                        Metadata = new Dictionary<string, object>
                        {
                            ["search_query"] = request.Prompt,
                            ["results_count"] = alternativeResults.Count,
                            ["provider"] = "Wikipedia",
                            ["search_strategy"] = "alternative",
                            ["search_time_ms"] = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2)
                        }
                    };
                }

                return new AIResponse
                {
                    Content = GenerateNoResultsMessage(request.Prompt),
                    IsSuccess = true,
                    ProcessingTime = stopwatch.Elapsed,
                    Metadata = new Dictionary<string, object>
                    {
                        ["search_query"] = request.Prompt,
                        ["results_count"] = 0,
                        ["provider"] = "Wikipedia"
                    }
                };
            }

            // Format the response with actual Wikipedia content
            var response = FormatWikipediaResponse(request.Prompt, searchResults);

            stopwatch.Stop();

            return new AIResponse
            {
                Content = response,
                IsSuccess = true,
                ProcessingTime = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["search_query"] = request.Prompt,
                    ["results_count"] = searchResults.Count,
                    ["provider"] = "Wikipedia",
                    ["search_time_ms"] = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Wikipedia search response for query: {Query}", request.Prompt);
            stopwatch.Stop();

            return new AIResponse
            {
                Content = "I encountered an error while searching Wikipedia. Please try again.",
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<List<WikipediaResult>> SearchWikipediaAsync(string query)
    {
        var results = new List<WikipediaResult>();

        try
        {
            // Clean up the query for better search results
            var cleanQuery = CleanSearchQuery(query);

            // Use Wikipedia's OpenSearch API for better compatibility
            var searchUrl = $"https://en.wikipedia.org/w/api.php?action=opensearch&search={HttpUtility.UrlEncode(cleanQuery)}&limit=3&namespace=0&format=json";
            _logger.LogInformation("Searching Wikipedia with URL: {SearchUrl}", searchUrl);

            var searchResponse = await _httpClient.GetStringAsync(searchUrl);
            var searchData = JsonSerializer.Deserialize<JsonElement[]>(searchResponse);

            if (searchData != null && searchData.Length >= 4)
            {
                var titles = searchData[1].EnumerateArray().ToArray();
                var descriptions = searchData[2].EnumerateArray().ToArray();
                var urls = searchData[3].EnumerateArray().ToArray();

                for (int i = 0; i < Math.Min(titles.Length, 3); i++)
                {
                    try
                    {
                        var title = titles[i].GetString() ?? "";
                        var description = descriptions[i].GetString() ?? "";
                        var url = urls[i].GetString() ?? "";

                        if (!string.IsNullOrEmpty(title))
                        {
                            // Try to get more detailed summary
                            _logger.LogDebug("Fetching detailed summary for: {Title}", title);
                            var detailedResult = await GetDetailedSummaryAsync(title);

                            if (detailedResult != null && !string.IsNullOrEmpty(detailedResult.Extract))
                            {
                                _logger.LogDebug("Successfully retrieved extract for {Title}: {ExtractLength} characters", title, detailedResult.Extract.Length);
                            }
                            else
                            {
                                _logger.LogDebug("No extract found for {Title}, using description: {Description}", title, description);
                            }

                            results.Add(new WikipediaResult
                            {
                                Title = title,
                                Extract = detailedResult?.Extract ?? description,
                                Url = url,
                                Thumbnail = detailedResult?.Thumbnail
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing Wikipedia search result at index {Index}", i);
                    }
                }
            }

            _logger.LogInformation("Found {Count} Wikipedia results for query: {Query}", results.Count, query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Wikipedia for query: {Query}", query);
        }

        return results;
    }

    private async Task<WikipediaDetailedResult?> GetDetailedSummaryAsync(string title)
    {
        try
        {
            // Use Wikipedia API with extracts for more reliable content retrieval
            var extractUrl = $"https://en.wikipedia.org/w/api.php?action=query&format=json&titles={HttpUtility.UrlEncode(title)}&prop=extracts|pageimages&exintro=true&explaintext=true&exsectionformat=plain&piprop=thumbnail&pithumbsize=300";

            var response = await _httpClient.GetAsync(extractUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Could not fetch extract for Wikipedia page: {Title} (Status: {StatusCode})", title, response.StatusCode);
                return null;
            }

            var extractResponse = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Wikipedia extract response for {Title}: {Response}", title, extractResponse);

            var extractData = JsonSerializer.Deserialize<WikipediaExtractResponse>(extractResponse);

            if (extractData?.Query?.Pages != null)
            {
                var page = extractData.Query.Pages.Values.FirstOrDefault();
                if (page != null && !string.IsNullOrEmpty(page.Extract))
                {
                    return new WikipediaDetailedResult
                    {
                        Extract = page.Extract,
                        Thumbnail = page.Thumbnail?.Source
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error fetching detailed extract for Wikipedia page: {Title}", title);
        }

        return null;
    }

    private string FormatWikipediaResponse(string query, List<WikipediaResult> results)
    {
        var response = new StringBuilder();

        // Start with a clean HTML structure
        response.AppendLine("<div class='wikipedia-results'>");
        response.AppendLine($"<div class='search-header'>");
        response.AppendLine($"<h3>📚 Wikipedia Search Results</h3>");
        response.AppendLine($"<p class='search-query'>Results for: <em>\"{System.Web.HttpUtility.HtmlEncode(query)}\"</em></p>");
        response.AppendLine("</div>");

        response.AppendLine("<div class='results-container'>");

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];

            response.AppendLine("<div class='wikipedia-result'>");

            // Title with link
            response.AppendLine($"<h4 class='result-title'>");
            response.AppendLine($"<a href='{result.Url}' target='_blank' class='wikipedia-link'>");
            response.AppendLine($"{i + 1}. {System.Web.HttpUtility.HtmlEncode(result.Title)}");
            response.AppendLine("</a>");
            response.AppendLine("</h4>");

            // Content area with thumbnail and extract
            response.AppendLine("<div class='result-content'>");

            // Thumbnail if available
            if (!string.IsNullOrEmpty(result.Thumbnail))
            {
                response.AppendLine("<div class='result-with-image'>");
                response.AppendLine($"<img src='{result.Thumbnail}' alt='{System.Web.HttpUtility.HtmlEncode(result.Title)}' class='result-thumbnail' />");
                response.AppendLine("<div class='result-text'>");
            }
            else
            {
                response.AppendLine("<div class='result-text'>");
            }

            // Extract/description
            if (!string.IsNullOrEmpty(result.Extract))
            {
                var extract = result.Extract.Length > 250
                    ? result.Extract.Substring(0, 250) + "..."
                    : result.Extract;
                response.AppendLine($"<p class='result-extract'>{System.Web.HttpUtility.HtmlEncode(extract)}</p>");
            }

            // Read more link
            response.AppendLine($"<a href='{result.Url}' target='_blank' class='read-more-link'>Read full article →</a>");

            response.AppendLine("</div>"); // Close result-text

            if (!string.IsNullOrEmpty(result.Thumbnail))
            {
                response.AppendLine("</div>"); // Close result-with-image
            }

            response.AppendLine("</div>"); // Close result-content
            response.AppendLine("</div>"); // Close wikipedia-result
        }

        response.AppendLine("</div>"); // Close results-container

        // Footer
        response.AppendLine("<div class='wikipedia-footer'>");
        response.AppendLine("<hr class='separator' />");
        response.AppendLine("<p class='footer-note'>");
        response.AppendLine("💡 <em>Click on any article title or \"Read full article\" link to view the complete Wikipedia page.</em>");
        response.AppendLine("</p>");
        response.AppendLine("<p class='source-attribution'>");
        response.AppendLine("📖 <small>All content sourced from <a href='https://wikipedia.org' target='_blank'>Wikipedia, the free encyclopedia</a>.</small>");
        response.AppendLine("</p>");
        response.AppendLine("</div>");

        response.AppendLine("</div>"); // Close wikipedia-results

        return response.ToString();
    }

    private string CleanSearchQuery(string query)
    {
        // Remove common question words and improve search terms
        var cleanQuery = query.ToLowerInvariant().Trim();

        // Remove greeting patterns at the beginning
        cleanQuery = System.Text.RegularExpressions.Regex.Replace(cleanQuery, @"^(hi|hello|hey|greetings),?\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Handle conversational patterns
        var conversationalPatterns = new[]
        {
            (@"can you tell me about\s+(.*)", "$1"),
            (@"tell me about\s+(.*)", "$1"),
            (@"i want to know about\s+(.*)", "$1"),
            (@"i'd like to learn about\s+(.*)", "$1"),
            (@"please explain\s+(.*)", "$1"),
            (@"explain\s+(.*)", "$1"),
            (@"what do you know about\s+(.*)", "$1"),
            (@"give me information about\s+(.*)", "$1"),
            (@"information about\s+(.*)", "$1"),
            (@"search for\s+(.*)", "$1"),
            (@"look up\s+(.*)", "$1"),
            (@"find\s+(.*)", "$1")
        };

        foreach (var (pattern, replacement) in conversationalPatterns)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (regex.IsMatch(cleanQuery))
            {
                cleanQuery = regex.Replace(cleanQuery, replacement).Trim();
                break;
            }
        }

        // Handle common question patterns with better logic
        var questionPatterns = new[]
        {
            ("what is ", 8),
            ("what are ", 9),
            ("who is ", 7),
            ("who are ", 8),
            ("where is ", 9),
            ("where are ", 10),
            ("how does ", 9),
            ("how do ", 7),
            ("how is ", 7),
            ("when was ", 9),
            ("when were ", 10),
            ("when is ", 8),
            ("why is ", 7),
            ("why are ", 8),
            ("why do ", 7),
            ("why does ", 9),
            ("which is ", 9),
            ("which are ", 10)
        };

        foreach (var (pattern, length) in questionPatterns)
        {
            if (cleanQuery.StartsWith(pattern))
            {
                cleanQuery = cleanQuery.Substring(length).Trim();
                break;
            }
        }

        // Remove question marks and other punctuation that might interfere
        cleanQuery = cleanQuery.Replace("?", "").Replace("!", "").Replace(",", "").Trim();

        // Handle articles and common prefixes
        if (cleanQuery.StartsWith("the ") && cleanQuery.Length > 4)
        {
            cleanQuery = cleanQuery.Substring(4);
        }
        else if (cleanQuery.StartsWith("a ") && cleanQuery.Length > 2)
        {
            cleanQuery = cleanQuery.Substring(2);
        }
        else if (cleanQuery.StartsWith("an ") && cleanQuery.Length > 3)
        {
            cleanQuery = cleanQuery.Substring(3);
        }

        // Handle specific topic mappings for better search results
        var topicMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"computer scientist", "Computer scientist"},
            {"matter", "Matter (physics)"},
            {"ai", "Artificial intelligence"},
            {"artificial intelligence", "Artificial intelligence"},
            {"machine learning", "Machine learning"},
            {"neural network", "Neural network"},
            {"neural networks", "Neural network"},
            {"deep learning", "Deep learning"},
            {"quantum computing", "Quantum computing"},
            {"climate change", "Climate change"},
            {"global warming", "Global warming"},
            {"covid", "COVID-19"},
            {"coronavirus", "COVID-19"},
            {"space", "Outer space"},
            {"universe", "Universe"},
            {"solar system", "Solar System"},
            {"black hole", "Black hole"},
            {"black holes", "Black hole"},
            {"dna", "DNA"},
            {"rna", "RNA"},
            {"evolution", "Evolution"},
            {"photosynthesis", "Photosynthesis"},
            {"gravity", "Gravity"},
            {"einstein", "Albert Einstein"},
            {"newton", "Isaac Newton"},
            {"shakespeare", "William Shakespeare"},
            {"leonardo da vinci", "Leonardo da Vinci"}
        };

        if (topicMappings.TryGetValue(cleanQuery, out var mappedTopic))
        {
            return mappedTopic;
        }

        // If the query is too generic or empty after cleaning, provide a fallback
        if (string.IsNullOrWhiteSpace(cleanQuery) || cleanQuery.Length < 2)
        {
            return "Wikipedia"; // Fallback to Wikipedia main page
        }

        // Capitalize first letter for better Wikipedia search results
        if (cleanQuery.Length > 0)
        {
            cleanQuery = char.ToUpperInvariant(cleanQuery[0]) + cleanQuery.Substring(1);
        }

        return cleanQuery;
    }

    private async Task<List<WikipediaResult>> TryAlternativeSearchAsync(string originalQuery)
    {
        var results = new List<WikipediaResult>();

        try
        {
            // Try different search strategies
            var alternativeQueries = GenerateAlternativeQueries(originalQuery);

            foreach (var altQuery in alternativeQueries)
            {
                _logger.LogDebug("Trying alternative search query: {Query}", altQuery);
                var altResults = await SearchWikipediaAsync(altQuery);

                if (altResults.Count > 0)
                {
                    _logger.LogInformation("Alternative search successful with query: {Query}, found {Count} results", altQuery, altResults.Count);
                    return altResults;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during alternative search for query: {Query}", originalQuery);
        }

        return results;
    }

    private List<string> GenerateAlternativeQueries(string originalQuery)
    {
        var alternatives = new List<string>();
        var cleanQuery = originalQuery.ToLowerInvariant().Trim();

        // Remove common question words more aggressively
        var questionWords = new[] { "what is", "what are", "who is", "who are", "where is", "when was", "how does", "why is" };

        foreach (var questionWord in questionWords)
        {
            if (cleanQuery.StartsWith(questionWord + " "))
            {
                var withoutQuestion = cleanQuery.Substring(questionWord.Length + 1).Trim();

                // Remove articles
                if (withoutQuestion.StartsWith("a "))
                    withoutQuestion = withoutQuestion.Substring(2);
                else if (withoutQuestion.StartsWith("an "))
                    withoutQuestion = withoutQuestion.Substring(3);
                else if (withoutQuestion.StartsWith("the "))
                    withoutQuestion = withoutQuestion.Substring(4);

                if (!string.IsNullOrWhiteSpace(withoutQuestion))
                {
                    alternatives.Add(withoutQuestion);

                    // Try capitalizing first letter for proper nouns
                    if (withoutQuestion.Length > 0)
                    {
                        var capitalized = char.ToUpper(withoutQuestion[0]) + withoutQuestion.Substring(1);
                        alternatives.Add(capitalized);
                    }
                }
                break;
            }
        }

        // Add some specific mappings for common queries
        if (cleanQuery.Contains("computer scientist"))
        {
            alternatives.AddRange(new[] { "Computer science", "Computer scientist", "Alan Turing", "Ada Lovelace" });
        }
        else if (cleanQuery.Contains("scientist"))
        {
            alternatives.AddRange(new[] { "Scientist", "Science", "Albert Einstein", "Marie Curie" });
        }
        else if (cleanQuery.Contains("programmer") || cleanQuery.Contains("developer"))
        {
            alternatives.AddRange(new[] { "Computer programming", "Software development", "Programming" });
        }

        // Remove duplicates and empty strings
        return alternatives.Where(q => !string.IsNullOrWhiteSpace(q)).Distinct().ToList();
    }

    private string GenerateNoResultsMessage(string originalQuery)
    {
        var suggestions = new List<string>();
        var cleanQuery = originalQuery.ToLowerInvariant();

        // Provide contextual suggestions based on the query
        if (cleanQuery.Contains("computer") || cleanQuery.Contains("programming") || cleanQuery.Contains("software"))
        {
            suggestions.AddRange(new[] { "Computer science", "Programming", "Software engineering", "Alan Turing" });
        }
        else if (cleanQuery.Contains("scientist"))
        {
            suggestions.AddRange(new[] { "Science", "Albert Einstein", "Marie Curie", "Isaac Newton" });
        }
        else if (cleanQuery.Contains("history"))
        {
            suggestions.AddRange(new[] { "History", "World War II", "Ancient Rome", "Renaissance" });
        }
        else
        {
            suggestions.AddRange(new[] { "Science", "Technology", "History", "Geography" });
        }

        var suggestionLinks = suggestions.Take(4).Select(s =>
            $"<a href='https://en.wikipedia.org/wiki/{Uri.EscapeDataString(s)}' target='_blank' class='suggestion-link'>{s}</a>"
        );

        return $@"
            <div class='no-results-message'>
                <div class='no-results-icon'>🔍</div>
                <h3>No Results Found</h3>
                <p>I couldn't find any Wikipedia articles for <em>""{System.Web.HttpUtility.HtmlEncode(originalQuery)}""</em>.</p>
                <div class='search-suggestions'>
                    <h4>Try searching for:</h4>
                    <div class='suggestions-list'>
                        {string.Join(" • ", suggestionLinks)}
                    </div>
                </div>
                <div class='search-tips'>
                    <h4>Search Tips:</h4>
                    <ul>
                        <li>Check your spelling</li>
                        <li>Try more specific terms</li>
                        <li>Use different keywords</li>
                        <li>Try searching for broader topics</li>
                    </ul>
                </div>
            </div>";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// Simplified Wikipedia API response models
public class WikipediaSummary
{
    public string? Title { get; set; }
    public string? Extract { get; set; }
    public WikipediaContentUrls? ContentUrls { get; set; }
    public WikipediaThumbnail? Thumbnail { get; set; }
}

public class WikipediaContentUrls
{
    public WikipediaDesktopUrls? Desktop { get; set; }
}

public class WikipediaDesktopUrls
{
    public string? Page { get; set; }
}

public class WikipediaThumbnail
{
    public string? Source { get; set; }
}

public class WikipediaResult
{
    public string Title { get; set; } = string.Empty;
    public string Extract { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Thumbnail { get; set; }
}

public class WikipediaDetailedResult
{
    public string? Extract { get; set; }
    public string? Thumbnail { get; set; }
}

// New classes for Wikipedia extract API
public class WikipediaExtractResponse
{
    [JsonPropertyName("query")]
    public WikipediaQuery? Query { get; set; }
}

public class WikipediaQuery
{
    [JsonPropertyName("pages")]
    public Dictionary<string, WikipediaPage>? Pages { get; set; }
}

public class WikipediaPage
{
    [JsonPropertyName("pageid")]
    public int PageId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("extract")]
    public string? Extract { get; set; }

    [JsonPropertyName("thumbnail")]
    public WikipediaThumbnail? Thumbnail { get; set; }
}
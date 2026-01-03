using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Xians.Agent.Sample.WebAgent;

public static class GoogleSearchCapability
{
    [Description("Search the web for a query and return the content as markdown")]
    public static async Task<string> WebSearch(
        [Description("Query to search for")] string query, 
        [Description("Number of results to return")] int numResults = 10)
    {
        Console.WriteLine($"[WebSearch] Starting web search for query: '{query}', numResults: {numResults}");
        
        try
        {
            // Get API key from environment
            var apiKey = Environment.GetEnvironmentVariable("VALUESERP_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("[WebSearch] ERROR: VALUESERP_API_KEY environment variable is not set");
                return "Error: VALUESERP_API_KEY environment variable is not set.";
            }

            using var searchEngine = new ValueSerpSearchEngine(apiKey);
            
            Console.WriteLine("[WebSearch] Executing search...");
            var searchResult = await searchEngine.SearchAsync(query, numResults);
            
            Console.WriteLine($"[WebSearch] Search completed successfully, found {searchResult.Items.Count} results");
            return FormatResultsAsMarkdown(searchResult, query);
        }
        catch (SearchException ex)
        {
            Console.WriteLine($"[WebSearch] Search error: {ex.Message}");
            Console.WriteLine($"[WebSearch] Stack trace: {ex.StackTrace}");
            return $"Search error: {ex.Message}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebSearch] Unexpected error: {ex.Message}");
            Console.WriteLine($"[WebSearch] Stack trace: {ex.StackTrace}");
            return $"Unexpected error: {ex.Message}";
        }
    }

    private static string FormatResultsAsMarkdown(SearchResult searchResult, string query)
    {
        var markdown = new StringBuilder();
        
        markdown.AppendLine($"# Search Results for: {query}");
        markdown.AppendLine();

        if (searchResult.SearchInformation?.TotalResults != null)
        {
            markdown.AppendLine($"*Total Results: {searchResult.SearchInformation.TotalResults}*");
            markdown.AppendLine();
        }

        if (searchResult.Items.Count == 0)
        {
            markdown.AppendLine("No results found.");
            return markdown.ToString();
        }

        for (int i = 0; i < searchResult.Items.Count; i++)
        {
            var item = searchResult.Items[i];
            
            markdown.AppendLine($"## {i + 1}. {item.Title ?? "No Title"}");
            
            if (!string.IsNullOrWhiteSpace(item.Link))
            {
                markdown.AppendLine($"**URL:** {item.Link}");
            }
            
            if (!string.IsNullOrWhiteSpace(item.Snippet))
            {
                markdown.AppendLine($"**Snippet:** {item.Snippet}");
            }
            
            markdown.AppendLine();
        }

        return markdown.ToString();
    }
}

// Search-related types
internal record SearchItem(
    string? Title,
    string? Link,
    string? Snippet
);

internal record SearchInformation(
    string? TotalResults
);

internal record SearchResult(
    List<SearchItem> Items,
    SearchInformation? SearchInformation
);

// Search interface
internal interface ISearchEngine : IDisposable
{
    Task<SearchResult> SearchAsync(string query, int numResults = 10);
}

// Search exception
internal class SearchException : Exception
{
    public SearchException(string message) : base(message) { }
    public SearchException(string message, Exception innerException) : base(message, innerException) { }
}

internal class ValueSerpSearchEngine : ISearchEngine, IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.valueserp.com/search";
    private bool _disposed;
    private readonly string _apiKey;

    public ValueSerpSearchEngine(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
    }

    public async Task<SearchResult> SearchAsync(string query, int numResults = 10)
    {
        ValidateParameters(query, numResults);

        var url = BuildRequestUrl(query);
        Console.WriteLine($"[ValueSerpSearchEngine] API URL: {url}");

        try
        {
            Console.WriteLine($"[ValueSerpSearchEngine] Sending GET request to ValueSerp API");
            var response = await _httpClient.GetAsync(url);
            Console.WriteLine($"[ValueSerpSearchEngine] Response status: {response.StatusCode}");
            
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[ValueSerpSearchEngine] Response content length: {content.Length} characters");
            
            var root = JsonDocument.Parse(content).RootElement;

            var items = ParseSearchItems(root, numResults);
            var searchInfo = ParseSearchInformation(root);

            Console.WriteLine($"[ValueSerpSearchEngine] Parsed {items.Count} search items");
            return new SearchResult(items, searchInfo);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[ValueSerpSearchEngine] HTTP Request failed: {ex.Message}");
            throw new SearchException("Failed to connect to VALUE SERP API", ex);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[ValueSerpSearchEngine] JSON parsing failed: {ex.Message}");
            throw new SearchException("Failed to parse search results", ex);
        }
        catch (Exception ex) when (ex is not SearchException)
        {
            Console.WriteLine($"[ValueSerpSearchEngine] Unexpected error: {ex.Message}");
            throw new SearchException("An unexpected error occurred during search", ex);
        }
    }

    private void ValidateParameters(string query, int numResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query cannot be empty", nameof(query));
        }

        if (numResults < 1 || numResults > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(numResults), "Number of results must be between 1 and 10");
        }
    }

    private string BuildRequestUrl(string query)
    {
        var encodedQuery = HttpUtility.UrlEncode(query);
        return $"{BaseUrl}?api_key={_apiKey}&q={encodedQuery}";
    }

    private List<SearchItem> ParseSearchItems(JsonElement root, int numResults)
    {
        if (!root.TryGetProperty("organic_results", out var organicResultsElement))
        {
            return new List<SearchItem>();
        }

        return organicResultsElement.EnumerateArray()
            .Take(numResults)
            .Select(item => new SearchItem(
                GetPropertySafe(item, "title"),
                GetPropertySafe(item, "link"),
                GetPropertySafe(item, "snippet")
            )).ToList();
    }

    private SearchInformation? ParseSearchInformation(JsonElement root)
    {
        return root.TryGetProperty("search_information", out var searchInfoElement)
            ? new SearchInformation(GetPropertySafe(searchInfoElement, "total_results"))
            : null;
    }

    private static string? GetPropertySafe(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null
            };
        }
        return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
    }

    ~ValueSerpSearchEngine()
    {
        Dispose(false);
    }
}


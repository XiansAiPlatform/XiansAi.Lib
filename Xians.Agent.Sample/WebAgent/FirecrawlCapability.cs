using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Xians.Agent.Sample.WebAgent;

public static class FirecrawlCapability
{
    [Description("Scrape/Crawl/Read a webpage and return the content as markdown.")]
    public static async Task<string> WebScrape(
        [Description("URL to scrape")] string url, 
        [Description("Extract only main content, excluding navigation and sidebars (default: true)")] bool onlyMainContent = true, 
        [Description("Maximum age of cached content in milliseconds (default: 0 = Always fetch fresh content)")] long maxAge = 0)
    {
        Console.WriteLine($"[WebScrape] Starting scrape for URL: '{url}', onlyMainContent: {onlyMainContent}, maxAge: {maxAge}");
        
        // Validate URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsValidFirecrawlUrl(uri))
        {
            Console.WriteLine($"[WebScrape] ERROR: Invalid URL provided: '{url}'");
            return "Error: Invalid URL provided";
        }

        Console.WriteLine($"[WebScrape] URL validated successfully: {uri}");

        var apiKey = Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("[WebScrape] ERROR: FIRECRAWL_API_KEY environment variable is not set");
            return "Error: FIRECRAWL_API_KEY environment variable is not set";
        }

        Console.WriteLine("[WebScrape] API key found");

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        var requestPayload = CreateAdvancedScrapePayload(uri, ["markdown"], onlyMainContent, maxAge);
        
        var json = JsonSerializer.Serialize(requestPayload);
        Console.WriteLine($"[WebScrape] Request payload: {json}");
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        const int maxRetries = 2;
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"[WebScrape] Attempt {attempt + 1}/{maxRetries}");
                var apiUrl = BuildApiUrl("scrape");
                Console.WriteLine($"[WebScrape] Sending POST request to: {apiUrl}");
                
                var response = await httpClient.PostAsync(apiUrl, content);
                Console.WriteLine($"[WebScrape] Response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[WebScrape] ERROR: API request failed with status {response.StatusCode}");
                    Console.WriteLine($"[WebScrape] Error content: {errorContent}");
                    
                    var exception = new HttpRequestException($"Firecrawl API request failed with status {response.StatusCode}: {errorContent}");
                    
                    if (attempt < maxRetries - 1 && (int)response.StatusCode >= 500)
                    {
                        Console.WriteLine($"[WebScrape] Server error detected, will retry after delay...");
                        lastException = exception;
                        await Task.Delay(1000 * (attempt + 1));
                        continue;
                    }
                    
                    return $"Error: {exception.Message}";
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[WebScrape] Response content length: {responseContent.Length} characters");
                
                var responseJson = JsonDocument.Parse(responseContent);
                
                if (responseJson.RootElement.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("markdown", out var markdownElement))
                {
                    var markdown = markdownElement.GetString() ?? string.Empty;
                    Console.WriteLine($"[WebScrape] Successfully extracted markdown, length: {markdown.Length} characters");
                    return markdown;
                }
                
                Console.WriteLine($"[WebScrape] ERROR: Unexpected response format - no markdown property found");
                Console.WriteLine($"[WebScrape] Response content: {responseContent}");
                return "Error: Unexpected response format from Firecrawl API";
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                Console.WriteLine($"[WebScrape] HTTP Request exception on attempt {attempt + 1}: {ex.Message}");
                Console.WriteLine($"[WebScrape] Will retry after delay...");
                lastException = ex;
                await Task.Delay(1000 * (attempt + 1));
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebScrape] Unexpected exception: {ex.Message}");
                Console.WriteLine($"[WebScrape] Stack trace: {ex.StackTrace}");
                return $"Error: Failed to scrape website: {ex.Message}";
            }
        }
        
        Console.WriteLine($"[WebScrape] All retry attempts exhausted");
        return $"Error: Failed to scrape website after {maxRetries} attempts: {lastException?.Message}";
    }

    [Description("Scrape a webpage and return extracted links with advanced parsing options.")]
    public static async Task<List<Uri>> ScrapeLinksFromWebpage(
        [Description("URL to scrape")] string url, 
        [Description("Extract only main content, excluding navigation and sidebars (default: true)")] bool onlyMainContent = true, 
        [Description("Maximum age of cached content in milliseconds (default: 0 = Always fetch fresh content)")] long maxAge = 0)
    {
        Console.WriteLine($"[ScrapeLinks] Starting link scrape for URL: '{url}'");
        
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsValidFirecrawlUrl(uri))
        {
            Console.WriteLine($"[ScrapeLinks] ERROR: Invalid URL provided: '{url}'");
            throw new ArgumentException("Invalid URL provided", nameof(url));
        }

        Console.WriteLine($"[ScrapeLinks] URL validated successfully: {uri}");

        var apiKey = Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("[ScrapeLinks] ERROR: FIRECRAWL_API_KEY environment variable is not set");
            throw new InvalidOperationException("FIRECRAWL_API_KEY environment variable is not set");
        }

        Console.WriteLine("[ScrapeLinks] API key found");

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        var requestPayload = CreateAdvancedScrapePayload(
            uri, 
            new[] { "markdown", "links" }, 
            onlyMainContent, 
            maxAge, 
            includeParsers: true);
        
        var json = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        const int maxRetries = 2;
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var apiUrl = BuildApiUrl("scrape");
                var response = await httpClient.PostAsync(apiUrl, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var exception = new HttpRequestException($"Firecrawl API request failed with status {response.StatusCode}: {errorContent}");
                    
                    if (attempt < maxRetries - 1 && (int)response.StatusCode >= 500)
                    {
                        lastException = exception;
                        await Task.Delay(1000 * (attempt + 1));
                        continue;
                    }
                    
                    throw exception;
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonDocument.Parse(responseContent);
                
                var links = new List<Uri>();
                if (responseJson.RootElement.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("links", out var linksElement))
                {
                    foreach (var linkElement in linksElement.EnumerateArray())
                    {
                        var linkString = linkElement.GetString();
                        if (!string.IsNullOrEmpty(linkString) && 
                            Uri.TryCreate(linkString, UriKind.Absolute, out var linkUri))
                        {
                            links.Add(linkUri);
                        }
                    }
                }
                
                return links;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                Console.WriteLine($"[ScrapeLinks] HTTP Request exception on attempt {attempt + 1}: {ex.Message}");
                Console.WriteLine($"[ScrapeLinks] Will retry after delay...");
                lastException = ex;
                await Task.Delay(1000 * (attempt + 1));
                continue;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[ScrapeLinks] HTTP Request exception (final): {ex.Message}");
                Console.WriteLine($"[ScrapeLinks] Stack trace: {ex.StackTrace}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScrapeLinks] Unexpected exception: {ex.Message}");
                Console.WriteLine($"[ScrapeLinks] Stack trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Failed to scrape website: {ex.Message}", ex);
            }
        }
        
        Console.WriteLine($"[ScrapeLinks] All retry attempts exhausted");
        throw new InvalidOperationException($"Failed to scrape website after {maxRetries} attempts: {lastException?.Message}", lastException);
    }

    [Description("Extract structured data from a webpage using a JSON schema.")]
    public static async Task<object> ExtractDataFromWebpage(
        [Description("URL to extract data from")] string url, 
        [Description("JSON schema defining the structure of data to extract")] string schema,
        [Description("Extract only main content, excluding navigation and sidebars (default: true)")] bool onlyMainContent = true,
        [Description("Request timeout in milliseconds (default: 120000)")] int timeout = 120000)
    {
        Console.WriteLine($"[ExtractData] Starting data extraction for URL: '{url}'");
        
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsValidFirecrawlUrl(uri))
        {
            Console.WriteLine($"[ExtractData] ERROR: Invalid URL provided: '{url}'");
            throw new ArgumentException("Invalid URL provided", nameof(url));
        }

        if (string.IsNullOrEmpty(schema))
        {
            Console.WriteLine("[ExtractData] ERROR: Schema not provided");
            throw new ArgumentException("Schema must be provided", nameof(schema));
        }

        Console.WriteLine($"[ExtractData] URL validated: {uri}");
        Console.WriteLine($"[ExtractData] Schema: {schema}");

        var apiKey = Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("[ExtractData] ERROR: FIRECRAWL_API_KEY environment variable is not set");
            throw new InvalidOperationException("FIRECRAWL_API_KEY environment variable is not set");
        }

        Console.WriteLine("[ExtractData] API key found");

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        var requestPayload = CreateScrapeWithJsonPayload(uri, schema, onlyMainContent, timeout);
        
        var json = JsonSerializer.Serialize(requestPayload);
        Console.WriteLine($"[ExtractData] Request payload: {json}");
        
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        const int maxRetries = 2;
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"[ExtractData] Attempt {attempt + 1}/{maxRetries}");
                var apiUrl = BuildApiUrl("scrape");
                Console.WriteLine($"[ExtractData] Sending POST request to: {apiUrl}");
                
                var response = await httpClient.PostAsync(apiUrl, content);
                Console.WriteLine($"[ExtractData] Response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ExtractData] ERROR: API request failed with status {response.StatusCode}");
                    Console.WriteLine($"[ExtractData] Error content: {errorContent}");
                    
                    var exception = new HttpRequestException($"Firecrawl API request failed with status {response.StatusCode}: {errorContent}");
                    
                    if (attempt < maxRetries - 1 && (int)response.StatusCode >= 500)
                    {
                        Console.WriteLine($"[ExtractData] Server error detected, will retry after delay...");
                        lastException = exception;
                        await Task.Delay(1000 * (attempt + 1));
                        continue;
                    }
                    
                    throw exception;
                }
                
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ExtractData] Response content length: {responseContent.Length} characters");
                
                var responseJson = JsonDocument.Parse(responseContent);
                
                if (responseJson.RootElement.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("json", out var jsonElement))
                {
                    Console.WriteLine($"[ExtractData] Successfully extracted JSON data");
                    return JsonSerializer.Deserialize<object>(jsonElement.GetRawText()) ?? new object();
                }
                
                if (responseJson.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var errorMessage = errorElement.GetString() ?? "Unknown error";
                    Console.WriteLine($"[ExtractData] API error: {errorMessage}");
                    throw new InvalidOperationException($"Firecrawl API error: {errorMessage}");
                }
                
                Console.WriteLine($"[ExtractData] ERROR: Unexpected response format");
                Console.WriteLine($"[ExtractData] Response content: {responseContent}");
                throw new InvalidOperationException($"No JSON data found in response. Full response: {responseContent}");
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                Console.WriteLine($"[ExtractData] HTTP Request exception on attempt {attempt + 1}: {ex.Message}");
                Console.WriteLine($"[ExtractData] Will retry after delay...");
                lastException = ex;
                await Task.Delay(1000 * (attempt + 1));
                continue;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[ExtractData] HTTP Request exception (final): {ex.Message}");
                Console.WriteLine($"[ExtractData] Stack trace: {ex.StackTrace}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtractData] Unexpected exception: {ex.Message}");
                Console.WriteLine($"[ExtractData] Stack trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Failed to extract data from website: {ex.Message}", ex);
            }
        }
        
        Console.WriteLine($"[ExtractData] All retry attempts exhausted");
        throw new InvalidOperationException($"Failed to extract data from website after {maxRetries} attempts: {lastException?.Message}", lastException);
    }

    // Internal helper methods
    private static bool IsValidFirecrawlUrl(Uri? url)
    {
        if (url == null)
            return false;

        return url.IsAbsoluteUri 
               && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps);
    }

    private static Uri BuildApiUrl(string endpoint = "scrape")
    {
        return new Uri($"https://api.firecrawl.dev/v2/{endpoint}");
    }

    private static object CreateAdvancedScrapePayload(
        Uri url, 
        string[] formats, 
        bool onlyMainContent = true, 
        long maxAge = 172800000, 
        bool includeParsers = true)
    {
        var payload = new
        {
            url = url.ToString(),
            onlyMainContent,
            maxAge,
            parsers = includeParsers ? new[] { "pdf" } : Array.Empty<string>(),
            formats
        };
        
        return payload;
    }

    private static object CreateScrapeWithJsonPayload(
        Uri url, 
        string schema, 
        bool onlyMainContent = true,
        int timeout = 120000)
    {
        if (!IsValidJson(schema))
        {
            throw new ArgumentException("Schema must be valid JSON", nameof(schema));
        }

        var payload = new
        {
            url = url.ToString(),
            onlyMainContent,
            timeout,
            formats = new[]
            {
                new
                {
                    type = "json",
                    schema = JsonSerializer.Deserialize<object>(schema),
                    prompt = "Extract the requested information from the webpage according to the provided schema."
                }
            }
        };
        
        return payload;
    }

    private static bool IsValidJson(string jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
            return false;

        try
        {
            JsonDocument.Parse(jsonString);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}


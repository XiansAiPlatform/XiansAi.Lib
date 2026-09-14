using System.ComponentModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

internal sealed class WebTools(string apiKey)
{
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("https://api.tavily.com/") };

    [Description("Search the web for current information. Returns titles, URLs, and relevant snippets.")]
    public async Task<string> SearchWeb(
        [Description("A focused web search query.")] string query,
        [Description("Number of results to return, from 1 to 5.")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 500)
            return "Search query must contain between 1 and 500 characters.";

        using var request = CreateRequest("search", new
        {
            query,
            search_depth = "basic",
            max_results = Math.Clamp(maxResults, 1, 5),
            include_answer = false,
            include_raw_content = false
        });

        try
        {
            using var response = await Http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var results = json.RootElement.GetProperty("results").EnumerateArray().Select(result => new
            {
                title = result.GetProperty("title").GetString(),
                url = result.GetProperty("url").GetString(),
                snippet = result.GetProperty("content").GetString()
            });
            return JsonSerializer.Serialize(results);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return $"Web search failed: {exception.Message}";
        }
    }

    [Description("Read and extract relevant Markdown content from a public HTTPS web page.")]
    public async Task<string> ReadWebPage(
        [Description("The public HTTPS URL to read.")] string url,
        [Description("Optional topic used to select the most relevant parts of the page.")] string? query = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return "A valid public HTTPS URL is required.";

        var body = new Dictionary<string, object>
        {
            ["urls"] = uri.AbsoluteUri,
            ["extract_depth"] = "basic",
            ["format"] = "markdown",
            ["timeout"] = 10
        };
        if (!string.IsNullOrWhiteSpace(query)) body["query"] = query[..Math.Min(query.Length, 500)];

        using var request = CreateRequest("extract", body);
        try
        {
            using var response = await Http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var result = json.RootElement.GetProperty("results").EnumerateArray().FirstOrDefault();
            if (result.ValueKind == JsonValueKind.Undefined) return "The page could not be read.";
            var content = result.GetProperty("raw_content").GetString() ?? string.Empty;
            return JsonSerializer.Serialize(new { url = uri.AbsoluteUri, content = content[..Math.Min(content.Length, 15_000)] });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return $"Web page reading failed: {exception.Message}";
        }
    }

    private HttpRequestMessage CreateRequest(string endpoint, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }
}

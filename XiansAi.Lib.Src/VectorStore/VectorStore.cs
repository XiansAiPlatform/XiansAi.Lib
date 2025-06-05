using System.Text.Json;
using System.Net.Http;

namespace XiansAi.VectorStore;
public static class VectorStore
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public static async Task<List<SearchResultItem>> SearchAsync(string query, string meilisearchUrl, string meilisearchApiKey, string indexName)
    {
        try
        {
            var searchResult = await MeilisearchService.SearchDocumentsAsync(
                _httpClient,
                query,
                meilisearchUrl,
                meilisearchApiKey,
                indexName);

            var results = new List<SearchResultItem>();
            if (searchResult.RootElement.TryGetProperty("hits", out var hitsElement) && hitsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var hit in hitsElement.EnumerateArray())
                {
                    results.Add(new SearchResultItem
                    {
                        Title = hit.TryGetProperty("title", out var title) ? title.GetString() : null,
                        Content = hit.TryGetProperty("content", out var content) ? content.GetString() : null,
                        Source = hit.TryGetProperty("source", out var source) ? source.GetString() : null
                    });
                }
            }
            return results;
        }
        catch (MeilisearchSearchException mex)
        {
            Console.WriteLine($"Meilisearch request failed: {mex.StatusCode}");
            Console.WriteLine($"Meilisearch response: {mex.ErrorContent}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred during search: {ex.Message}");
            throw;
        }
    }
}

public class MeilisearchConnectionConfig
{
    public required string Url { get; set; }
    public required string ApiKey { get; set; }
    public required string IndexName { get; set; }
}

public class SearchRequest
{
    public required string Query { get; set; }
    public required MeilisearchConnectionConfig MeilisearchConfig { get; set; }
}

public class CreateIndexRequest
{
    public required MeilisearchConnectionConfig MeilisearchConfig { get; set; }
}

public class SearchResultItem
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Source { get; set; }
}

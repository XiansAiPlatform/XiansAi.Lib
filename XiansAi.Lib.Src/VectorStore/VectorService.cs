using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace XiansAi.VectorStore;

public class MeilisearchSearchException : Exception
{
    public int StatusCode { get; }
    public string ErrorContent { get; }

    public MeilisearchSearchException(string message, int statusCode, string errorContent) : base(message)
    {
        StatusCode = statusCode;
        ErrorContent = errorContent;
    }

    public MeilisearchSearchException(string message, int statusCode, string errorContent, Exception innerException) : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorContent = errorContent;
    }
}

public static class MeilisearchService
{
    private static async Task<HttpResponseMessage> SendRequestAsync(HttpClient httpClient, HttpMethod method, string url, string apiKey, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (content != null)
        {
            request.Content = content;
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return await httpClient.SendAsync(request);
    }

    public static async Task<JsonDocument> SearchDocumentsAsync(HttpClient httpClient, string query, string baseUrl, string apiKey, string indexName)
    {
        var url = $"{baseUrl}/indexes/{indexName}/search";

        var searchPayload = new
        {
            q = query,
            hybrid = new
            {
                embedder = "products_openai"
            },
            limit = 5
        };

        var content = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await SendRequestAsync(httpClient, HttpMethod.Post, url, apiKey, content);
        
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Meilisearch search request failed: {response.StatusCode}");
            Console.WriteLine($"Meilisearch search response: {responseContent}");
            throw new MeilisearchSearchException($"Meilisearch search failed with status {response.StatusCode}", (int)response.StatusCode, responseContent);
        }

        return JsonDocument.Parse(responseContent);
    }
}

using System.Net;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Represents a response to a webhook request.
/// Contains HTTP-style response properties.
/// </summary>
public class WebhookResponse
{
    /// <summary>
    /// Gets or sets the HTTP status code for the response.
    /// </summary>
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    /// <summary>
    /// Gets or sets the content body of the response.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets the content type of the response (e.g., "application/json").
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Gets or sets the response headers.
    /// </summary>
    public Dictionary<string, string[]> Headers { get; set; } = new Dictionary<string, string[]>();

    /// <summary>
    /// Creates a successful JSON response with the specified content.
    /// </summary>
    /// <param name="content">The JSON content.</param>
    /// <returns>A WebhookResponse with status 200 OK.</returns>
    public static WebhookResponse Ok(string? content = null)
    {
        return new WebhookResponse
        {
            StatusCode = HttpStatusCode.OK,
            Content = content,
            ContentType = "application/json"
        };
    }

    /// <summary>
    /// Creates a successful JSON response with the specified data object.
    /// </summary>
    /// <param name="data">The data object to serialize as JSON.</param>
    /// <returns>A WebhookResponse with status 200 OK.</returns>
    public static WebhookResponse Ok(object data)
    {
        return new WebhookResponse
        {
            StatusCode = HttpStatusCode.OK,
            Content = System.Text.Json.JsonSerializer.Serialize(data),
            ContentType = "application/json"
        };
    }

    /// <summary>
    /// Creates an error response with the specified status code and message.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>A WebhookResponse with the specified error status.</returns>
    public static WebhookResponse Error(HttpStatusCode statusCode, string? message = null)
    {
        return new WebhookResponse
        {
            StatusCode = statusCode,
            Content = message != null 
                ? System.Text.Json.JsonSerializer.Serialize(new { error = message }) 
                : null,
            ContentType = "application/json"
        };
    }

    /// <summary>
    /// Creates a Bad Request (400) response.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A WebhookResponse with status 400 Bad Request.</returns>
    public static WebhookResponse BadRequest(string? message = null)
    {
        return Error(HttpStatusCode.BadRequest, message ?? "Bad Request");
    }

    /// <summary>
    /// Creates a Not Found (404) response.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A WebhookResponse with status 404 Not Found.</returns>
    public static WebhookResponse NotFound(string? message = null)
    {
        return Error(HttpStatusCode.NotFound, message ?? "Not Found");
    }

    /// <summary>
    /// Creates an Internal Server Error (500) response.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A WebhookResponse with status 500 Internal Server Error.</returns>
    public static WebhookResponse InternalServerError(string? message = null)
    {
        return Error(HttpStatusCode.InternalServerError, message ?? "Internal Server Error");
    }

    /// <summary>
    /// Applies this webhook response to an HTTP context (server-side).
    /// Sets the status code, content type, headers, and body.
    /// </summary>
    /// <param name="httpContext">The HTTP context to apply the response to.</param>
    public async Task ApplyToHttpContextAsync(object httpContext)
    {
        // Use reflection to work with HttpContext without direct dependency
        var httpContextType = httpContext.GetType();
        var responseProperty = httpContextType.GetProperty("Response");
        if (responseProperty == null)
        {
            throw new InvalidOperationException("HttpContext.Response property not found");
        }

        var response = responseProperty.GetValue(httpContext);
        if (response == null)
        {
            throw new InvalidOperationException("HttpContext.Response is null");
        }

        var responseType = response.GetType();

        // Set status code
        var statusCodeProperty = responseType.GetProperty("StatusCode");
        statusCodeProperty?.SetValue(response, (int)StatusCode);

        // Set content type
        var contentTypeProperty = responseType.GetProperty("ContentType");
        contentTypeProperty?.SetValue(response, ContentType);

        // Set headers
        if (Headers != null && Headers.Count > 0)
        {
            var headersProperty = responseType.GetProperty("Headers");
            var headers = headersProperty?.GetValue(response);
            
            if (headers != null)
            {
                var headersType = headers.GetType();
                var indexer = headersType.GetProperties()
                    .FirstOrDefault(p => p.GetIndexParameters().Length > 0);
                
                foreach (var header in Headers)
                {
                    indexer?.SetValue(headers, header.Value, new object[] { header.Key });
                }
            }
        }

        // Write content
        if (!string.IsNullOrEmpty(Content))
        {
            var bodyProperty = responseType.GetProperty("Body");
            var bodyStream = bodyProperty?.GetValue(response) as Stream;
            
            if (bodyStream != null)
            {
                var contentBytes = System.Text.Encoding.UTF8.GetBytes(Content);
                await bodyStream.WriteAsync(contentBytes, 0, contentBytes.Length);
            }
        }
    }
}


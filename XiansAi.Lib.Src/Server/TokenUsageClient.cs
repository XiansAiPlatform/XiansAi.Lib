using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiansAi.Exceptions;

namespace Server;

public class TokenUsageClient
{
    private static readonly Lazy<TokenUsageClient> _instance = new(() => new TokenUsageClient());
    public static TokenUsageClient Instance => _instance.Value;

    private readonly ILogger<TokenUsageClient> _logger = Globals.LogFactory.CreateLogger<TokenUsageClient>();

    private TokenUsageClient()
    {
    }

    public async Task EnsureWithinLimitAsync(CancellationToken cancellationToken = default)
    {
        if (!SecureApi.IsReady)
        {
            return;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            // Use the authenticated user from the certificate for quota enforcement
            var userId = AgentContext.UserId;
            var endpoint = $"/api/agent/usage/status?userId={Uri.EscapeDataString(userId)}";
            var response = await client.GetWithRetryAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var status = await response.Content.ReadFromJsonAsync<UsageStatusResponse>(cancellationToken: cancellationToken);
            if (status?.IsExceeded == true)
            {
                throw new TokenLimitExceededException("Token usage limit exceeded for this tenant/user.");
            }
        }
        catch (TokenLimitExceededException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify token usage for tenant {TenantId}, user {UserId}", AgentContext.TenantId, AgentContext.UserId);
        }
    }

    public async Task ReportAsync(TokenUsageReport report, CancellationToken cancellationToken = default)
    {
        if (!SecureApi.IsReady)
        {
            return;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var json = JsonContent.Create(report, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var response = await client.PostWithRetryAsync("/api/agent/usage/report", json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to report token usage. Status={StatusCode}, Payload={Payload}", response.StatusCode, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report token usage metrics.");
        }
    }

    public static long EstimateTokens(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        return Math.Max(1, content.Length / 4);
    }

    private sealed class UsageStatusResponse
    {
        public bool Enabled { get; set; }
        public bool IsExceeded { get; set; }
    }
}

public record TokenUsageReport(
    string? WorkflowId,
    string? RequestId,
    string? Model,
    long PromptTokens,
    long CompletionTokens,
    string Source,
    string? UserId,
    Dictionary<string, string>? Metadata);

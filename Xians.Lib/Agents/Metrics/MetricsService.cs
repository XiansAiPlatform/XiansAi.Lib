using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Metrics.Models;
using Xians.Lib.Common;

namespace Xians.Lib.Agents.Metrics;

/// <summary>
/// Core service for metrics reporting via HTTP client.
/// Shared by both MetricsActivityExecutor and MetricsCollection to avoid code duplication.
/// Handles direct HTTP client operations for reporting usage metrics.
/// </summary>
internal class MetricsService
{
    private readonly XiansAgent _agent;
    private readonly ILogger _logger;

    public MetricsService(XiansAgent agent, ILogger logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reports flexible usage metrics to the Xians platform server.
    /// This method is safe to call even if the HTTP service is not ready - it will log a warning and return.
    /// </summary>
    /// <param name="request">The usage report request containing metrics array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReportAsync(UsageReportRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_agent.HttpService == null)
            {
                _logger.LogDebug(
                    "HTTP service not available for usage reporting. Agent: {AgentName}", 
                    _agent.Name);
                return;
            }

            var client = _agent.HttpService.Client;
            var json = JsonContent.Create(request, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Add tenant header for system-scoped agents
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/agent/usage/report");
            httpRequest.Content = json;
            
            if (!string.IsNullOrEmpty(request.TenantId))
            {
                httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);
            }

            var response = await client.SendAsync(httpRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to report usage metrics. Status={StatusCode}, Payload={Payload}", 
                    response.StatusCode, 
                    payload);
            }
            else
            {
                _logger.LogDebug(
                    "Usage reported successfully: Model={Model}, MetricsCount={MetricsCount}",
                    request.Model,
                    request.Metrics?.Count ?? 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report usage metrics.");
        }
    }
}

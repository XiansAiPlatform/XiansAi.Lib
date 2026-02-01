using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Agents.Metrics.Models;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Temporal.Workflows.Usage;

/// <summary>
/// System activity for reporting usage metrics from workflows.
/// Automatically registered with all workflows.
/// Activities can perform non-deterministic operations like HTTP calls.
/// </summary>
public class UsageActivities
{
    private readonly ILogger<UsageActivities> _logger;

    public UsageActivities()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<UsageActivities>();
    }

    /// <summary>
    /// Reports usage metrics to the Xians platform.
    /// This activity wraps the MetricsService to allow workflows to track usage.
    /// </summary>
    /// <param name="request">Usage report request containing metrics and metadata.</param>
    [Activity]
    public async Task ReportUsageAsync(UsageReportRequest request)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "ReportUsage activity started: TenantId={TenantId}, Source={Source}, MetricsCount={MetricsCount}",
            request.TenantId,
            request.WorkflowType,
            request.Metrics.Count);

        try
        {
            // Get the current agent and use its MetricsService
            var agent = XiansContext.CurrentAgent;
            if (agent.HttpService == null)
            {
                ActivityExecutionContext.Current.Logger.LogWarning(
                    "HTTP service not available for usage reporting. Skipping metrics report.");
                return;
            }

            var metricsService = new Agents.Metrics.MetricsService(agent, _logger);
            await metricsService.ReportAsync(request);

            ActivityExecutionContext.Current.Logger.LogInformation(
                "Usage metrics reported successfully: TenantId={TenantId}, Source={Source}",
                request.TenantId,
                request.WorkflowType);
        }
        catch (Exception ex)
        {
            ActivityExecutionContext.Current.Logger.LogError(ex,
                "Error reporting usage metrics: TenantId={TenantId}, Source={Source}",
                request.TenantId,
                request.WorkflowType);
            throw;
        }
    }
}

using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Metrics.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Usage;

namespace Xians.Lib.Agents.Metrics;

/// <summary>
/// Activity executor for metrics operations.
/// Handles context-aware execution of metrics activities.
/// Eliminates duplication of Workflow.InWorkflow checks in MetricsCollection.
/// </summary>
internal class MetricsActivityExecutor : ContextAwareActivityExecutor<UsageActivities, MetricsService>
{
    private readonly XiansAgent _agent;

    public MetricsActivityExecutor(XiansAgent agent, ILogger logger)
        : base(logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    protected override MetricsService CreateService()
    {
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<MetricsService>();
        return new MetricsService(_agent, logger);
    }

    /// <summary>
    /// Reports usage metrics with automatic context detection.
    /// - In workflows: Uses UsageActivities (deterministic, no direct HTTP calls)
    /// - Outside workflows: Directly calls MetricsService (HTTP)
    /// </summary>
    /// <param name="request">The usage report request containing metrics and metadata.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReportAsync(UsageReportRequest request)
    {
        await ExecuteAsync(
            act => act.ReportUsageAsync(request),
            svc => svc.ReportAsync(request),
            operationName: "ReportUsage");
    }
}

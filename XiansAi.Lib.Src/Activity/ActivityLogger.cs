using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace XiansAi.Activity;

static class ActivityLogger
{
    private static readonly ILogger _logger;

    static ActivityLogger()
    {
        var logFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new ApiLoggerProvider("/api/client/logs"));
        });

        _logger = logFactory.CreateLogger("ActivityLogger");
        _logger.LogInformation("ActivityLogger initialized.");
    }

    public static void LogInformation(string message)
    {
        Console.WriteLine("logging info: " + message);

        if (ActivityExecutionContext.Current != null)
        {
            var info = ActivityExecutionContext.Current.Info;

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["TenantId"] = info.WorkflowNamespace,
                ["WorkflowId"] = info.WorkflowId,
                ["RunId"] = info.WorkflowRunId
            }))
            {
                _logger.LogInformation(message);
            }
        }
        else
        {
            _logger.LogInformation($"[No Workflow Context] {message}");
        }
    }

   public static void LogError(string message, Exception exception)
{
    Console.WriteLine("logging error: " + message);

    if (ActivityExecutionContext.Current != null)
    {
        var info = ActivityExecutionContext.Current.Info;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = info.WorkflowNamespace,
            ["WorkflowId"] = info.WorkflowId,
            ["RunId"] = info.WorkflowRunId
        }))
        {
            _logger.LogError(exception, message);
        }
    }
    else
    {
        _logger.LogError(exception, $"[No Workflow Context] {message}");
    }
}

}

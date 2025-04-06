using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace XiansAi.Activity;

static class ActivityLogger
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger("ActivityLogger");

    public static void LogInformation(string message)
    {
        if (ActivityExecutionContext.Current != null) {
            ActivityExecutionContext.Current.Logger.LogInformation($"{message}");
        } else {
            _logger.LogInformation($"[No Workflow Context] {message}");
        }
    }

    public static void LogError(string message, Exception exception)
    {
        if (ActivityExecutionContext.Current != null) {
            ActivityExecutionContext.Current.Logger.LogError($"{message}", exception);
        } else {
            _logger.LogError($"[No Workflow Context] {message}", exception);
        }
    }
}

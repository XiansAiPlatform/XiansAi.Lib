using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace Xians.Lib.Agents;

/// <summary>
/// Context-aware logger that works in both workflow and non-workflow contexts.
/// Automatically uses Workflow.Logger when in workflow, otherwise uses standard logger.
/// </summary>
public static class XiansLogger
{
    private static ILogger? _fallbackLogger;
    
    private static ILogger FallbackLogger
    {
        get
        {
            if (_fallbackLogger == null)
            {
                _fallbackLogger = Common.LoggerFactory.Instance.CreateLogger("Xians");
            }
            return _fallbackLogger;
        }
    }

    /// <summary>
    /// Gets a context-aware logger.
    /// Returns Workflow.Logger if in workflow context, otherwise returns a standard logger.
    /// </summary>
    public static ILogger Current
    {
        get
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Logger;
            }
            else
            {
                return FallbackLogger;
            }
        }
    }

    /// <summary>
    /// Gets a context-aware logger for a specific type.
    /// </summary>
    public static ILogger GetLogger<T>()
    {
        if (Workflow.InWorkflow)
        {
            return Workflow.Logger;
        }
        else
        {
            return Common.LoggerFactory.CreateLogger<T>();
        }
    }

}


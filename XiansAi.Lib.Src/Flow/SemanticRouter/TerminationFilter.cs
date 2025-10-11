
using Microsoft.SemanticKernel;
using XiansAi.Logging;

public sealed class TerminationFilter : IAutoFunctionInvocationFilter
{
    private const int WARNING_CONSECUTIVE_CALLS = 5;
    private string? _lastFunctionKey;
    private int _consecutiveCallCount = 0;
    private int _maxConsecutiveCalls;
    private readonly Logger<TerminationFilter> _logger;
    private readonly object _lock = new();

    public TerminationFilter(int maxConsecutiveCalls)
    {
        _logger = Logger<TerminationFilter>.For();
        _maxConsecutiveCalls = maxConsecutiveCalls;
    }

    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        var pluginName = context.Function.PluginName;
        var functionName = context.Function.Name;
        var functionKey = $"{pluginName}.{functionName}";

        lock (_lock)
        {
            // Check if this is the same function as the last call
            if (functionKey == _lastFunctionKey)
            {
                _consecutiveCallCount++;
            }
            else
            {
                // Different function, reset counter
                _consecutiveCallCount = 1;
                _lastFunctionKey = functionKey;
            }

            _logger.LogDebug($"Function {functionKey} called consecutively {_consecutiveCallCount} times");

            // Print warning if more than 3 consecutive calls
            if (_consecutiveCallCount > WARNING_CONSECUTIVE_CALLS)
            {
                _logger.LogWarning($"Warning: Function {functionKey} has been called consecutively {_consecutiveCallCount} times. Will terminate if it is called {_maxConsecutiveCalls} times.");
            }

            // Check if we've exceeded the consecutive limit
            if (_consecutiveCallCount > _maxConsecutiveCalls)
            {
                _logger.LogWarning($"Terminating execution: Function {functionKey} has been called consecutively {_consecutiveCallCount} times, exceeding limit of {_maxConsecutiveCalls} times.");
                context.Terminate = true;
                throw new Exception($"Function {functionKey} has been called consecutively {_consecutiveCallCount} times, exceeding limit of {_maxConsecutiveCalls} times. Terminate the execution add return an error to the caller.");
            }
        }

        // Call the function
        await next(context);
    }
}
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Xians.Lib.Common;

/// <summary>
/// Generic retry policy executor for transient failures with exponential backoff.
/// </summary>
public class RetryPolicy
{
    private readonly ILogger? _logger;
    private readonly int _maxRetryAttempts;
    private readonly int _retryDelaySeconds;
    private readonly Func<Exception, bool>? _customTransientChecker;

    public RetryPolicy(
        int maxRetryAttempts, 
        int retryDelaySeconds, 
        ILogger? logger = null,
        Func<Exception, bool>? customTransientChecker = null)
    {
        _maxRetryAttempts = maxRetryAttempts;
        _retryDelaySeconds = retryDelaySeconds;
        _logger = logger;
        _customTransientChecker = customTransientChecker;
    }

    /// <summary>
    /// Executes an operation with exponential backoff retry logic.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetryAttempts)
        {
            attempt++;
            
            try
            {
                if (attempt > 1)
                {
                    var delay = CalculateBackoffDelay(attempt);
                    _logger?.LogInformation(
                        "Retrying operation (attempt {Attempt}/{MaxAttempts}) after {Delay}ms", 
                        attempt, _maxRetryAttempts, delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }

                var result = await operation();
                
                if (attempt > 1)
                {
                    _logger?.LogInformation("Operation succeeded on attempt {Attempt}", attempt);
                }
                
                return result;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt))
            {
                lastException = ex;
                _logger?.LogWarning(ex, 
                    "Operation failed on attempt {Attempt}/{MaxAttempts}: {Message}", 
                    attempt, _maxRetryAttempts, ex.Message);
            }
        }

        _logger?.LogError(lastException, "Operation failed after {MaxAttempts} attempts", _maxRetryAttempts);
        throw lastException ?? new InvalidOperationException("Operation failed after all retry attempts");
    }

    /// <summary>
    /// Executes an operation with retry logic (no return value).
    /// </summary>
    public async Task ExecuteAsync(Func<Task> operation)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        });
    }

    private bool ShouldRetry(Exception ex, int currentAttempt)
    {
        if (currentAttempt >= _maxRetryAttempts)
            return false;

        // Use custom checker if provided
        if (_customTransientChecker != null)
            return _customTransientChecker(ex);

        // Default transient exception detection
        return IsTransientException(ex);
    }

    private TimeSpan CalculateBackoffDelay(int attempt)
    {
        return TimeSpan.FromMilliseconds(
            _retryDelaySeconds * 1000 * Math.Pow(2, attempt - 2));
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx => IsTransientHttpException(httpEx),
            TaskCanceledException => true,
            SocketException => true,
            TimeoutException => true,
            _ => false
        };
    }

    private static bool IsTransientHttpException(HttpRequestException httpEx)
    {
        var message = httpEx.Message.ToLower();
        return message.Contains("timeout") || 
               message.Contains("connection") || 
               message.Contains("network") ||
               message.Contains("dns") ||
               message.Contains("ssl") ||
               message.Contains("certificate");
    }
}


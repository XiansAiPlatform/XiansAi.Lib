using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http.Json;
using Server;
using System.Net;
using XiansAi.Models;
using XiansAi.Flow;
using XiansAi;

public class ApiLoggerProvider : ILoggerProvider
{
    private readonly string _logApiUrl;

    public ApiLoggerProvider(string logApiUrl)
    {
        _logApiUrl = logApiUrl;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ApiLogger(_logApiUrl, SecureApi.Instance);
    }

    public void Dispose() { }
}

public class ApiLogger : ILogger
{
    private readonly string _logApiUrl;

    private readonly ISecureApiClient _secureApi;
    private static readonly AsyncLocal<IDisposable?> _currentScope = new AsyncLocal<IDisposable?>();
    private static readonly AsyncLocal<Dictionary<string, object>?> _currentContext = new AsyncLocal<Dictionary<string, object>?>();

    public ApiLogger(string logApiUrl, ISecureApiClient secureApi)
    {
        _logApiUrl = PlatformConfig.APP_SERVER_URL + logApiUrl;
        _secureApi = secureApi ??
           throw new ArgumentNullException(nameof(secureApi));
    }

    IDisposable ILogger.BeginScope<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object>> kvps)
        {
            var contextDict = kvps.ToDictionary(kv => kv.Key, kv => kv.Value);
            _currentContext.Value = contextDict;
        }

        var disposable = new ScopeDisposable(() => _currentContext.Value = null);
        _currentScope.Value = disposable;
        return disposable;
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var logMessage = formatter(state, exception);
        var context = _currentContext.Value;

        var tenantId = context?.GetValueOrDefault("TenantId")?.ToString() ?? "defaultTenantId";
        var workflowId = context?.GetValueOrDefault("WorkflowId")?.ToString() ?? "defaultWorkflowId";
        var workflowRunId = context?.GetValueOrDefault("WorkflowRunId")?.ToString() ?? "defaultWorkflowRunId";

        var log = new Log
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            Level = (XiansAi.Models.LogLevel)logLevel,
            Message = logMessage,
            WorkflowId = workflowId,
            WorkflowRunId = workflowRunId,
            Properties = null,
            Exception = exception?.ToString(),
            UpdatedAt = null
        };

        _ = Task.Run(async () =>
        {
            try
            {

                if (!_secureApi.IsReady)
                {
                    Console.Error.WriteLine("App server secure API is not available, upload of flow definition failed");
                    throw new InvalidOperationException("App server secure API is not available");
                }

                var client = _secureApi.Client;
                var response = await client.PostAsync(_logApiUrl, JsonContent.Create(log));

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Logger API failed with status {response.StatusCode}");
                }
                else
                {
                    Console.WriteLine($"Logger API succeeded: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Logger exception: {ex.Message}");
            }
        });
    }

    private class ScopeDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public ScopeDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}

public class LogContext
{
    public required string TenantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string RunId { get; set; }
}

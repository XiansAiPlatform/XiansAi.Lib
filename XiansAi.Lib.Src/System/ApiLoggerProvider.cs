using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using XiansAi.Models;
using System;
using XiansAi.Flow;

public class ApiLoggerProvider : ILoggerProvider
{
    private readonly string _logApiUrl;

    public ApiLoggerProvider(string logApiUrl)
    {
        _logApiUrl = logApiUrl;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ApiLogger(_logApiUrl);
    }

    public void Dispose() { }
}

public class ApiLogger : ILogger
{
    private readonly string _logApiUrl;
    private static readonly AsyncLocal<IDisposable?> _currentScope = new AsyncLocal<IDisposable?>();
    private static readonly AsyncLocal<Dictionary<string, object>?> _currentContext = new AsyncLocal<Dictionary<string, object>?>();

    public ApiLogger(string logApiUrl)
    {
        _logApiUrl = PlatformConfig.APP_SERVER_URL + logApiUrl;
    }

    public IDisposable BeginScope<TState>(TState state)
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
        var runId = context?.GetValueOrDefault("RunId")?.ToString() ?? "defaultRunId";

        var log = new Log
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            Level = (XiansAi.Models.LogLevel)logLevel,
            Message = logMessage,
            WorkflowId = workflowId,
            RunId = runId,
            Properties = null,
            Exception = exception?.ToString(),
            UpdatedAt = null
        };

        Console.WriteLine($"Log: {logMessage}");
        Console.WriteLine($"Log Context: {tenantId}, {workflowId}, {runId}");

        // Send to API (mocked or real)
        _ = Task.Run(async () => {
            try
            {
                using var httpClient = new HttpClient();
                var content = new StringContent(JsonConvert.SerializeObject(log), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(_logApiUrl, content);
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
    public string TenantId { get; set; }
    public string WorkflowId { get; set; }
    public string RunId { get; set; }
}

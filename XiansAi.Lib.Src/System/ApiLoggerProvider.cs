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
    private readonly AsyncLocal<LogContext> _logContext = new AsyncLocal<LogContext>();

    public ApiLogger(string logApiUrl)
    {
        _logApiUrl = PlatformConfig.APP_SERVER_URL+logApiUrl;
    }

    // Set the context for the logger. This can be called before logging.
    public void SetContext(string tenantId, string workflowId, string runId)
    {
        _logContext.Value = new LogContext
        {
            TenantId = tenantId,
            WorkflowId = workflowId,
            RunId = runId
        };
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        // Optional: implement if needed to add context to the logs
        return null; 
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return true; // You can customize this to filter log levels
    }

    public async void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var logMessage = formatter(state, exception);

        // Get the dynamic context (tenantId, workflowId, runId)
        var context = _logContext.Value;

        // If context is null, use default values
        if (context == null)
        {
            context = new LogContext
            {
                TenantId = "defaultTenantId",
                WorkflowId = "defaultWorkflowId",
                RunId = "defaultRunId"
            };
        }

        var log = new Log
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = context.TenantId,  // Use dynamic tenant ID
            CreatedAt = DateTime.UtcNow,
            Level = (XiansAi.Models.LogLevel)logLevel, // Map to your custom LogLevel
            Message = logMessage,
            WorkflowId = context.WorkflowId, // Use dynamic workflow ID
            RunId = context.RunId, // Use dynamic run ID
            Properties = null, // Add custom properties if needed
            Exception = exception?.ToString(),
            UpdatedAt = null
        };

        Console.WriteLine($"Log: {logMessage}"); // Optional: log to console for debugging
        Console.WriteLine($"Log Context: {context.TenantId}, {context.WorkflowId}, {context.RunId}"); // Optional: log context for debugging
    
        _ = Task.Run(async () =>
        {
            try
            {
                using var httpClient = new HttpClient();
                var content = new StringContent(JsonConvert.SerializeObject(log), Encoding.UTF8, "application/json");
                //var response = await httpClient.PostAsync(_logApiUrl, content);
                // mock response for testing
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK); // Mock response for testing
                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Logger API failed with status {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Logger exception: {ex.Message}");
            }
        });
      }
}

public class LogContext
{
    public string TenantId { get; set; }
    public string WorkflowId { get; set; }
    public string RunId { get; set; }
}

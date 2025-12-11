using System.Diagnostics;
using System.Linq;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Diagnostics.Metrics;
using Temporalio.Extensions.OpenTelemetry;

namespace XiansAi.Telemetry;

/// <summary>
/// Extension methods for configuring OpenTelemetry observability for SemanticKernel
/// Supports configuration via environment variables:
/// - OPENTELEMETRY_ENABLED (default: false)
/// - OPENTELEMETRY_ENDPOINT (required if enabled - no default, must be explicitly set)
///   Examples:
///     - Development: http://aspire-dashboard:18889
///     - Production: http://otel-collector:4317
/// - OPENTELEMETRY_SERVICE_NAME (default: XiansAi.Lib)
/// </summary>
public static class OpenTelemetryExtensions
{
    private static TracerProvider? _tracerProvider;
    private static MeterProvider? _meterProvider;
    private static bool _isInitialized = false;
    private static readonly object _lock = new object();
    
    // ActivitySource for creating parent spans for SemanticKernel operations
    private static readonly ActivitySource ActivitySource = new("XiansAi.SemanticKernel");
    
    // ActivitySource for creating spans for Temporal operations
    private static readonly ActivitySource TemporalActivitySource = new("XiansAi.Temporal");

    // Meter for custom metrics
    private static readonly Meter Meter = new("XiansAi.Lib");
    
    // Counter for LLM token usage
    private static readonly Counter<long> TokenUsageCounter = Meter.CreateCounter<long>(
        "xians_ai.llm.tokens.usage", 
        description: "Number of tokens used in LLM interactions");

    /// <summary>
    /// Ensures OpenTelemetry is initialized. Called automatically by all public methods.
    /// Thread-safe and idempotent - safe to call multiple times.
    /// </summary>
    private static void EnsureInitialized()
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                return;
            }

            var enabled = Environment.GetEnvironmentVariable("OPENTELEMETRY_ENABLED")?.ToLower() == "true";
            if (!enabled)
            {
                Console.WriteLine("[OpenTelemetry] OpenTelemetry is disabled (OPENTELEMETRY_ENABLED not set to 'true')");
                _isInitialized = true;
                return;
            }

            var serviceName = Environment.GetEnvironmentVariable("OPENTELEMETRY_SERVICE_NAME") ?? "XiansAi.Lib";
            var serviceVersion = typeof(OpenTelemetryExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0";
            var otlpEndpoint = Environment.GetEnvironmentVariable("OPENTELEMETRY_ENDPOINT");
            
            if (string.IsNullOrEmpty(otlpEndpoint))
            {
                Console.WriteLine("[OpenTelemetry] WARNING: OPENTELEMETRY_ENDPOINT not set - OpenTelemetry will not export traces/metrics");
                Console.WriteLine("[OpenTelemetry] To enable OpenTelemetry export, set OPENTELEMETRY_ENDPOINT environment variable");
                Console.WriteLine("[OpenTelemetry] Examples:");
                Console.WriteLine("[OpenTelemetry]   - Development: export OPENTELEMETRY_ENDPOINT=http://aspire-dashboard:18889");
                Console.WriteLine("[OpenTelemetry]   - Production: export OPENTELEMETRY_ENDPOINT=http://otel-collector:4317");
                _isInitialized = true;
                return;
            }

            try
            {
                Console.WriteLine($"[OpenTelemetry] Initializing OpenTelemetry for service: {serviceName}");
                Console.WriteLine($"[OpenTelemetry] OTLP Endpoint: {otlpEndpoint}");
                
                // Configure tracing
                _tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .ConfigureResource(resource => resource
                        .AddService(
                            serviceName: serviceName,
                            serviceVersion: serviceVersion,
                            serviceInstanceId: Environment.MachineName)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                            ["host.name"] = Environment.MachineName,
                        }))
                    // Add SemanticKernel activity sources (try multiple patterns)
                    .AddSource("Microsoft.SemanticKernel.*")
                    .AddSource("XiansAi.*") // Custom activity sources including XiansAi.SemanticKernel and XiansAi.Temporal
                    .AddSource(ActivitySource.Name) // Add our ActivitySource for parent spans
                    // Add TracingInterceptor ActivitySources for automatic Temporal spans
                    .AddSource(TracingInterceptor.ClientSource.Name)      // Client operations (StartWorkflow, Signal, etc.)
                    .AddSource(TracingInterceptor.WorkflowsSource.Name)  // Workflow execution spans
                    .AddSource(TracingInterceptor.ActivitiesSource.Name)   // Activity execution spans ⭐ THIS CAPTURES ACTIVITY SPANS
                    .AddSource("Temporal.*") // Fallback pattern for any other Temporal sources
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        
                        // Enrich with request details
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("http.request.method", httpRequestMessage.Method.ToString());
                            activity.SetTag("http.request.url", httpRequestMessage.RequestUri?.ToString() ?? "");
                            activity.SetTag("http.scheme", httpRequestMessage.RequestUri?.Scheme ?? "");
                            activity.SetTag("http.host", httpRequestMessage.RequestUri?.Host ?? "");
                            
                            // Trace context propagation is now handled automatically by:
                            // 1. TracingInterceptor (ensures Activity.Current is set correctly in Temporal workflows)
                            // 2. HttpClient instrumentation (automatically injects traceparent header)
                            // No manual trace header injection needed
                            
                            // Extract tenant context from headers (if propagated from calling service)
                            if (httpRequestMessage.Headers.Contains("X-Tenant-Id"))
                            {
                                var tenantId = httpRequestMessage.Headers.GetValues("X-Tenant-Id").FirstOrDefault();
                                if (!string.IsNullOrEmpty(tenantId))
                                {
                                    activity.SetTag("tenant.id", tenantId);
                                }
                            }
                            
                            // Extract user context from headers (if propagated from calling service)
                            if (httpRequestMessage.Headers.Contains("X-User-Id"))
                            {
                                var userId = httpRequestMessage.Headers.GetValues("X-User-Id").FirstOrDefault();
                                if (!string.IsNullOrEmpty(userId))
                                {
                                    activity.SetTag("user.id", userId);
                                }
                            }
                            
                            // Try to get tenant/user context from AgentContext if available
                            try
                            {
                                var tenantId = AgentContext.TenantId;
                                if (!string.IsNullOrEmpty(tenantId))
                                {
                                    activity.SetTag("tenant.id", tenantId);
                                }
                            }
                            catch
                            {
                                // Silently ignore if AgentContext.TenantId throws
                            }
                            
                            try
                            {
                                var userId = AgentContext.UserId;
                                if (!string.IsNullOrEmpty(userId))
                                {
                                    activity.SetTag("user.id", userId);
                                }
                            }
                            catch
                            {
                                // Silently ignore if AgentContext.UserId throws
                            }
                        };
                        
                        // Enrich with response details
                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                        {
                            activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
                            
                            // Add response size if available
                            if (httpResponseMessage.Content != null && httpResponseMessage.Content.Headers.ContentLength.HasValue)
                            {
                                activity.SetTag("http.response.size", httpResponseMessage.Content.Headers.ContentLength.Value);
                            }
                            
                            // Extract LLM token usage from OpenAI/Azure OpenAI API responses
                            var requestUri = httpResponseMessage.RequestMessage?.RequestUri?.ToString() ?? "";
                            if ((requestUri.Contains("openai.com") || requestUri.Contains("azure.com") || requestUri.Contains("/chat/completions") || requestUri.Contains("/completions")) 
                                && httpResponseMessage.IsSuccessStatusCode)
                            {
                                // Try to extract model from request URL
                                if (requestUri.Contains("/deployments/"))
                                {
                                    // Azure OpenAI format: /openai/deployments/{deployment}/chat/completions
                                    var parts = requestUri.Split('/');
                                    var deploymentIndex = Array.IndexOf(parts, "deployments");
                                    if (deploymentIndex >= 0 && deploymentIndex + 1 < parts.Length)
                                    {
                                        activity.SetTag("llm.deployment", parts[deploymentIndex + 1]);
                                    }
                                }
                                
                                // Mark this as an LLM call for easier filtering
                                activity.SetTag("llm.provider", requestUri.Contains("azure.com") ? "azure-openai" : "openai");
                                activity.SetTag("llm.operation", requestUri.Contains("/chat/completions") ? "chat_completion" : "completion");
                            }
                        };
                    })
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                        // Note: Exporter failures won't break execution - spans will be buffered or dropped silently
                    })
                    .Build();

                Console.WriteLine($"[OpenTelemetry] ✓ TracerProvider initialized successfully");

                // Configure metrics
                _meterProvider = Sdk.CreateMeterProviderBuilder()
                    .ConfigureResource(resource => resource
                        .AddService(
                            serviceName: serviceName,
                            serviceVersion: serviceVersion,
                            serviceInstanceId: Environment.MachineName))
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Microsoft.SemanticKernel*")
                    .AddMeter("XiansAi.*") 
                    .AddMeter("XiansAi.Lib") // Explicitly add our custom meter
                    .AddMeter("Temporal.*") // Custom meters
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                        // Note: Exporter failures won't break execution - metrics will be buffered or dropped silently
                    })
                    .Build();

                Console.WriteLine($"[OpenTelemetry] ✓ MeterProvider initialized successfully");
                Console.WriteLine($"[OpenTelemetry] ✓ OpenTelemetry fully enabled for {serviceName}");
                Console.WriteLine($"[OpenTelemetry]   - Service: {serviceName} v{serviceVersion}");
                Console.WriteLine($"[OpenTelemetry]   - OTLP Endpoint: {otlpEndpoint}");
                Console.WriteLine($"[OpenTelemetry]   - Activity Sources: Microsoft.SemanticKernel.*, XiansAi.*, Temporal.*");
                Console.WriteLine($"[OpenTelemetry]   - Meters: Microsoft.SemanticKernel*, XiansAi.*, Temporal.*");
                Console.WriteLine($"[OpenTelemetry]   - Note: If collector is unreachable, traces/metrics will be buffered or dropped (non-blocking)");
            }
            catch (Exception ex)
            {
                // OpenTelemetry initialization failures should NOT break the application
                // Log warning and continue - application will work without telemetry
                Console.WriteLine($"[OpenTelemetry] ⚠ WARNING: Failed to initialize OpenTelemetry: {ex.Message}");
                Console.WriteLine($"[OpenTelemetry] ⚠ Application will continue without telemetry export");
                Console.WriteLine($"[OpenTelemetry] ⚠ Stack trace: {ex.StackTrace}");
                // Don't rethrow - let application continue
            }

            _isInitialized = true;
        }
    }

    /// <summary>
    /// Adds OpenTelemetry instrumentation for SemanticKernel
    /// Reads configuration from environment variables
    /// </summary>
    public static IKernelBuilder AddOpenTelemetry(this IKernelBuilder builder)
    {
        // Ensure OpenTelemetry is initialized
        EnsureInitialized();

        // Enable Semantic Kernel sensitive data diagnostics if OpenTelemetry is enabled
        if (_isInitialized && _tracerProvider != null)
        {
            AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);
        }

        return builder;
    }
    
    /// <summary>
    /// Creates a parent span for SemanticKernel operations that links to the current trace context.
    /// This ensures all LLM calls are grouped under a single trace.
    /// </summary>
    /// <param name="operationName">Name of the operation (e.g., "SemanticKernel.Completion", "SemanticKernel.Route")</param>
    /// <param name="tags">Optional tags to add to the span</param>
    /// <returns>An Activity that should be disposed when the operation completes</returns>
    public static System.Diagnostics.Activity? StartSemanticKernelOperation(string operationName, Dictionary<string, object>? tags = null)
    {
        // Ensure OpenTelemetry is initialized before creating activities
        EnsureInitialized();
        
        if (!_isInitialized || _tracerProvider == null)
        {
            return null;
        }
        
        // TracingInterceptor handles trace context restoration automatically
        // Fallback: If Activity.Current is null but we have a stored activity in AgentContext, restore it
        var currentActivity = System.Diagnostics.Activity.Current;
        if (currentActivity == null && AgentContext.CurrentTraceActivity != null)
        {
            // Manually set Activity.Current from AgentContext as fallback
            // This doesn't "start" the activity again, just sets the ambient context
            System.Diagnostics.Activity.Current = AgentContext.CurrentTraceActivity;
            currentActivity = AgentContext.CurrentTraceActivity;
        }
        
        var parentContext = currentActivity?.Context ?? default;
        
        // StartActivity automatically uses Activity.Current as parent if it exists
        // This ensures we link to the parent trace from the server request
        // If Activity.Current is null, we'll create a root span (which breaks trace continuity)
        // Explicitly pass parent context to ensure we link even if Activity.Current is somehow lost
        
        // StartActivity automatically uses Activity.Current as parent if it exists
        // If Activity.Current is null, we'll create a root span (which breaks trace continuity)
        // Explicitly pass parent context to ensure we link even if Activity.Current is somehow lost
        var activity = ActivitySource.StartActivity(
            operationName, 
            System.Diagnostics.ActivityKind.Internal,
            parentContext: parentContext);
        
        // If no activity was created (sampling or not started), return null
        if (activity == null)
        {
            return null;
        }
        
        // Activity is already started and set as Activity.Current by StartActivity()
        // All child operations (including HTTP calls) will now use this as parent
        {
            activity.SetTag("semantic_kernel.operation", operationName);
            
            // Add tenant/user context if available
            try
            {
                var tenantId = AgentContext.TenantId;
                if (!string.IsNullOrEmpty(tenantId))
                {
                    activity.SetTag("tenant.id", tenantId);
                }
            }
            catch
            {
                // Silently ignore if AgentContext.TenantId throws
            }
            
            try
            {
                var userId = AgentContext.UserId;
                if (!string.IsNullOrEmpty(userId))
                {
                    activity.SetTag("user.id", userId);
                }
            }
            catch
            {
                // Silently ignore if AgentContext.UserId throws
            }
            
            // Add custom tags if provided
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    activity.SetTag(tag.Key, tag.Value);
                }
            }
        }
        
        return activity;
    }
    
    /// <summary>
    /// Restores trace context from workflow memo and sets it as the current activity context.
    /// This should be called at the start of workflow signal handlers to continue the trace.
    /// Restores trace context from workflow memo or signal payload to continue the trace from the server request.
    /// NOTE: TracingInterceptor handles this automatically, but this method is kept as a fallback for edge cases.
    /// </summary>
    /// <param name="traceParentFromPayload">Optional traceparent from signal payload (for existing workflows)</param>
    /// <param name="traceStateFromPayload">Optional tracestate from signal payload (for existing workflows)</param>
    [Obsolete("TracingInterceptor handles trace context restoration automatically. This method is kept as a fallback only.")]
    public static void RestoreTraceContextFromMemo(string? traceParentFromPayload = null, string? traceStateFromPayload = null)
    {
        // CRITICAL: Ensure OpenTelemetry is initialized FIRST before attempting to restore trace context
        EnsureInitialized();
        
        // TracingInterceptor handles trace context restoration automatically
        // This method is kept as a fallback for edge cases where TracingInterceptor might not work
        try
        {
            string? traceParent = traceParentFromPayload;
            string? traceState = traceStateFromPayload;
            
            // First try to get from signal payload (for existing workflows)
            if (string.IsNullOrEmpty(traceParent))
            {
                // Fall back to workflow memo (for new workflows)
                var memo = Temporalio.Workflows.Workflow.Memo;
                if (memo != null && memo.TryGetValue("traceparent", out var traceParentValue))
                {
                    traceParent = traceParentValue?.Payload?.Data?.ToStringUtf8()?.Replace("\"", "");
                    
                    // Get tracestate from memo if not provided in payload
                    if (string.IsNullOrEmpty(traceState) && memo.TryGetValue("tracestate", out var traceStateValue))
                    {
                        traceState = traceStateValue?.Payload?.Data?.ToStringUtf8()?.Replace("\"", "");
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(traceParent))
            {
                // Parse traceparent: 00-{TraceId}-{SpanId}-{Flags}
                var parts = traceParent.Split('-');
                if (parts.Length == 4 && parts[0] == "00")
                {
                    var traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
                    var spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
                    var flags = parts[3] == "01" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;
                    
                    var activityContext = new ActivityContext(
                        traceId,
                        spanId,
                        flags,
                        traceState,
                        isRemote: true);
                    
                    var activity = TemporalActivitySource.StartActivity(
                        "Temporal.Workflow.RestoreContext",
                        System.Diagnostics.ActivityKind.Internal,
                        parentContext: activityContext);
                    
                    if (activity != null)
                    {
                        if (!string.IsNullOrEmpty(traceState))
                        {
                            activity.TraceStateString = traceState;
                        }
                        
                        // Store in AgentContext as fallback for Temporal workflow async boundaries
                        AgentContext.CurrentTraceActivity = activity;
                    }
                }
            }
        }
        catch
        {
            // Silently fail - TracingInterceptor should handle trace context restoration
        }
    }
    
    /// <summary>
    /// Creates a span for Temporal operations that links to the current trace context.
    /// </summary>
    /// <param name="operationName">Name of the operation (e.g., "Temporal.ExecuteWorkflow", "Temporal.CreateSchedule")</param>
    /// <param name="tags">Optional tags to add to the span</param>
    /// <returns>An Activity that should be disposed when the operation completes</returns>
    public static System.Diagnostics.Activity? StartTemporalOperation(
        string operationName, 
        Dictionary<string, object>? tags = null)
    {
        // Ensure OpenTelemetry is initialized before creating activities
        EnsureInitialized();
        
        if (!_isInitialized || _tracerProvider == null)
        {
            return null;
        }
        
        // Get the current activity context (should be set by TracingInterceptor)
        // This ensures we link to the parent trace from the server request
        var currentActivity = System.Diagnostics.Activity.Current;
        var parentContext = currentActivity?.Context ?? default;
        
        if (currentActivity != null)
        {
        }
        else
        {
        }
        
        var activity = TemporalActivitySource.StartActivity(
            operationName,
            System.Diagnostics.ActivityKind.Internal,
            parentContext: parentContext);
        
        if (activity == null)
        {
            return null;
        }
        
        activity.SetTag("temporal.operation", operationName);
        
        // Add tenant/user context if available
        try
        {
            var tenantId = AgentContext.TenantId;
            if (!string.IsNullOrEmpty(tenantId))
            {
                activity.SetTag("tenant.id", tenantId);
            }
        }
        catch
        {
            // Silently ignore if AgentContext.TenantId throws
        }
        
        try
        {
            var userId = AgentContext.UserId;
            if (!string.IsNullOrEmpty(userId))
            {
                activity.SetTag("user.id", userId);
            }
        }
        catch
        {
            // Silently ignore if AgentContext.UserId throws
        }
        
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }
        
        
        return activity;
    }

    /// <summary>
    /// Records LLM token usage metrics with the provided tags.
    /// </summary>
    /// <param name="tokens">Number of tokens used</param>
    /// <param name="tags">Tags to associate with the metric (e.g., model, operation)</param>
    public static void RecordTokenUsage(long tokens, Dictionary<string, object> tags)
    {
        // Ensure OpenTelemetry is initialized
        EnsureInitialized();
        
        if (!_isInitialized || _meterProvider == null)
        {
            return;
        }

        try
        {
            // Create TagList from dictionary
            var tagList = new TagList();
            foreach (var tag in tags)
            {
                tagList.Add(tag.Key, tag.Value);
            }
            
            // Add tenant/user context if available and not already in tags
            if (!tags.ContainsKey("tenant.id"))
            {
                try
                {
                    var tenantId = AgentContext.TenantId;
                    if (!string.IsNullOrEmpty(tenantId))
                    {
                        tagList.Add("tenant.id", tenantId);
                    }
                }
                catch { /* Ignore */ }
            }
            
            if (!tags.ContainsKey("user.id"))
            {
                try
                {
                    var userId = AgentContext.UserId;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        tagList.Add("user.id", userId);
                    }
                }
                catch { /* Ignore */ }
            }
            
            TokenUsageCounter.Add(tokens, tagList);
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenTelemetry] ERROR recording token usage: {ex.Message}");
        }
    }
}


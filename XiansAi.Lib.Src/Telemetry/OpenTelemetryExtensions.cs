using System.Diagnostics;
using System.Linq;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Diagnostics.Metrics;

namespace XiansAi.Telemetry;

/// <summary>
/// Extension methods for configuring OpenTelemetry observability for SemanticKernel
/// Supports configuration via environment variables:
/// - OPENTELEMETRY_ENABLED (default: false)
/// - OPENTELEMETRY_ENDPOINT (default: http://localhost:4317)
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
            var otlpEndpoint = Environment.GetEnvironmentVariable("OPENTELEMETRY_ENDPOINT") ?? "http://localhost:4317";

            try
            {
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
                    .AddSource("Temporal.*") // Explicitly add Temporal ActivitySource
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
                            
                            // CRITICAL FIX: Explicitly inject trace headers if Activity.Current is not the right one
                            // This happens when making HTTP callbacks from Temporal workflows
                            var currentActivity = System.Diagnostics.Activity.Current;
                            System.Diagnostics.Activity? traceActivity = null;
                            
                            // If there's no current activity or it's a different trace, use AgentContext
                            if (currentActivity == null && AgentContext.CurrentTraceActivity != null)
                            {
                                traceActivity = AgentContext.CurrentTraceActivity;
                                Console.WriteLine($"[OpenTelemetry] HTTP request: Activity.Current is NULL, injecting trace headers from AgentContext");
                                Console.WriteLine($"  - Request URI: {httpRequestMessage.RequestUri}");
                                Console.WriteLine($"  - AgentContext TraceId: {traceActivity.TraceId}");
                            }
                            else if (currentActivity != null && AgentContext.CurrentTraceActivity != null 
                                     && currentActivity.TraceId != AgentContext.CurrentTraceActivity.TraceId)
                            {
                                // Current activity exists but is a different trace - use AgentContext
                                traceActivity = AgentContext.CurrentTraceActivity;
                                Console.WriteLine($"[OpenTelemetry] HTTP request: Activity.Current has different TraceId, injecting from AgentContext");
                                Console.WriteLine($"  - Request URI: {httpRequestMessage.RequestUri}");
                                Console.WriteLine($"  - Current TraceId: {currentActivity.TraceId}");
                                Console.WriteLine($"  - AgentContext TraceId: {traceActivity.TraceId}");
                            }
                            
                            // Inject trace headers if we have a trace activity to propagate
                            if (traceActivity != null)
                            {
                                var traceparent = $"00-{traceActivity.TraceId}-{traceActivity.SpanId}-{(traceActivity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00")}";
                                var tracestate = traceActivity.TraceStateString;
                                
                                // Add or replace traceparent header
                                if (httpRequestMessage.Headers.Contains("traceparent"))
                                {
                                    httpRequestMessage.Headers.Remove("traceparent");
                                }
                                httpRequestMessage.Headers.Add("traceparent", traceparent);
                                
                                // Add tracestate if it exists
                                if (!string.IsNullOrEmpty(tracestate))
                                {
                                    if (httpRequestMessage.Headers.Contains("tracestate"))
                                    {
                                        httpRequestMessage.Headers.Remove("tracestate");
                                    }
                                    httpRequestMessage.Headers.Add("tracestate", tracestate);
                                }
                                
                                Console.WriteLine($"  - ✓ Injected traceparent: {traceparent}");
                                if (!string.IsNullOrEmpty(tracestate))
                                {
                                    Console.WriteLine($"  - ✓ Injected tracestate: {tracestate}");
                                }
                            }
                            
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
                    })
                    .Build();

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
                        Console.WriteLine($"[OpenTelemetry] Metrics OTLP Exporter configured for {otlpEndpoint}");
                    })
                    .Build();

                Console.WriteLine($"[OpenTelemetry] MeterProvider built. Meters: Microsoft.SemanticKernel*, XiansAi.*, XiansAi.Lib, Temporal.*");

                Console.WriteLine($"[OpenTelemetry] OpenTelemetry enabled for {serviceName} - exporting to {otlpEndpoint}");
                Console.WriteLine($"[OpenTelemetry] Tracing configured with SemanticKernel and HTTP client instrumentation");
                Console.WriteLine($"[OpenTelemetry] Activity sources: Microsoft.SemanticKernel, Microsoft.SemanticKernel.Core, Microsoft.SemanticKernel.Connectors.OpenAI, Microsoft.SemanticKernel.Connectors.AzureOpenAI, Microsoft.SemanticKernel.Agents, XiansAi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenTelemetry] Failed to initialize OpenTelemetry: {ex.Message}");
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
            Console.WriteLine($"[OpenTelemetry] DIAGNOSTIC: StartSemanticKernelOperation('{operationName}') called but OpenTelemetry not initialized");
            return null;
        }
        
        // CRITICAL FIX: Temporal workflows don't preserve Activity.Current across async boundaries
        // If Activity.Current is null but we have a stored activity in AgentContext, restore it
        var currentActivity = System.Diagnostics.Activity.Current;
        if (currentActivity == null && AgentContext.CurrentTraceActivity != null)
        {
            Console.WriteLine($"[OpenTelemetry] DIAGNOSTIC: Activity.Current is NULL, restoring from AgentContext");
            Console.WriteLine($"  - AgentContext.CurrentTraceActivity: TraceId={AgentContext.CurrentTraceActivity.TraceId}, SpanId={AgentContext.CurrentTraceActivity.SpanId}");
            
            // Manually set Activity.Current from AgentContext
            // This doesn't "start" the activity again, just sets the ambient context
            System.Diagnostics.Activity.Current = AgentContext.CurrentTraceActivity;
            currentActivity = AgentContext.CurrentTraceActivity;
            
            Console.WriteLine($"  - ✓ Activity.Current restored from AgentContext");
        }
        
        // DIAGNOSTIC: Log current activity state BEFORE creating new operation
        Console.WriteLine($"[OpenTelemetry] DIAGNOSTIC: StartSemanticKernelOperation('{operationName}') - Current Activity State:");
        if (currentActivity != null)
        {
            Console.WriteLine($"  - Activity.Current EXISTS");
            Console.WriteLine($"  - TraceId: {currentActivity.TraceId}");
            Console.WriteLine($"  - SpanId: {currentActivity.SpanId}");
            Console.WriteLine($"  - ParentSpanId: {currentActivity.ParentSpanId}");
            Console.WriteLine($"  - OperationName: {currentActivity.OperationName}");
            Console.WriteLine($"  - Source.Name: {currentActivity.Source.Name}");
            Console.WriteLine($"  - ActivityTraceFlags: {currentActivity.ActivityTraceFlags}");
        }
        else
        {
            Console.WriteLine($"  - Activity.Current is NULL");
            Console.WriteLine($"  - WARNING: No parent activity found - will create ROOT span");
            Console.WriteLine($"  - This breaks trace continuity from HTTP request!");
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
            Console.WriteLine($"[OpenTelemetry] DIAGNOSTIC: Failed to create activity for '{operationName}' - likely sampled out or provider not configured");
            return null;
        }
        
        // DIAGNOSTIC: Log newly created activity
        Console.WriteLine($"[OpenTelemetry] DIAGNOSTIC: SemanticKernel activity created successfully:");
        Console.WriteLine($"  - New TraceId: {activity.TraceId}");
        Console.WriteLine($"  - New SpanId: {activity.SpanId}");
        Console.WriteLine($"  - ParentSpanId: {activity.ParentSpanId}");
        Console.WriteLine($"  - Parent Link: {(activity.ParentSpanId != default ? "LINKED to parent" : "NO PARENT (root span)")}");
        Console.WriteLine($"  - Activity.Current now points to this new activity");
        
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
    /// </summary>
    /// <summary>
    /// Restores trace context from workflow memo or signal payload and sets it as the current activity context.
    /// This should be called at the start of workflow signal handlers to continue the trace.
    /// </summary>
    /// <param name="traceParentFromPayload">Optional traceparent from signal payload (for existing workflows)</param>
    /// <param name="traceStateFromPayload">Optional tracestate from signal payload (for existing workflows)</param>
    public static void RestoreTraceContextFromMemo(string? traceParentFromPayload = null, string? traceStateFromPayload = null)
    {
        // CRITICAL: Ensure OpenTelemetry is initialized FIRST before attempting to restore trace context
        EnsureInitialized();
        
        Console.WriteLine($"[OpenTelemetry] DIAGNOSTIC: RestoreTraceContextFromMemo() called");
        Console.WriteLine($"  - traceParentFromPayload: {traceParentFromPayload ?? "NULL"}");
        Console.WriteLine($"  - traceStateFromPayload: {traceStateFromPayload ?? "NULL"}");
        
        // Log Activity.Current BEFORE restoration
        var beforeActivity = System.Diagnostics.Activity.Current;
        Console.WriteLine($"[OpenTelemetry] DIAGNOSTIC: Activity.Current BEFORE restoration:");
        if (beforeActivity != null)
        {
            Console.WriteLine($"  - EXISTS: TraceId={beforeActivity.TraceId}, SpanId={beforeActivity.SpanId}, Source={beforeActivity.Source.Name}");
        }
        else
        {
            Console.WriteLine($"  - NULL (expected in Temporal workflow context)");
        }
        
        try
        {
            string? traceParent = traceParentFromPayload;
            string? traceState = traceStateFromPayload;
            string? source = null;
            
            // First try to get from signal payload (for existing workflows)
            if (!string.IsNullOrEmpty(traceParent))
            {
                source = "signal payload";
                Console.WriteLine($"[OpenTelemetry] Found traceparent in signal payload: {traceParent}");
            }
            else
            {
                // Fall back to workflow memo (for new workflows)
                var memo = Temporalio.Workflows.Workflow.Memo;
                if (memo != null && memo.TryGetValue("traceparent", out var traceParentValue))
                {
                    // Extract string value from IRawValue (same pattern as MemoUtil)
                    traceParent = traceParentValue?.Payload?.Data?.ToStringUtf8()?.Replace("\"", "");
                    source = "workflow memo";
                    
                    if (!string.IsNullOrEmpty(traceParent))
                    {
                        Console.WriteLine($"[OpenTelemetry] Found traceparent in memo: {traceParent}");
                        
                        // Get tracestate from memo if not provided in payload
                        if (string.IsNullOrEmpty(traceState) && memo.TryGetValue("tracestate", out var traceStateValue))
                        {
                            traceState = traceStateValue?.Payload?.Data?.ToStringUtf8()?.Replace("\"", "");
                        }
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
                    
                    // Create activity context (ActivityContext constructor: traceId, spanId, flags, traceState, isRemote)
                    var activityContext = new ActivityContext(
                        traceId,
                        spanId,
                        flags,
                        traceState,
                        isRemote: true);
                    
                    // Create and start a new activity with the restored parent context using TemporalActivitySource
                    // This ensures the activity is properly registered and exported
                    var parentId = $"00-{traceId}-{spanId}-{(flags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00")}";
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
                        
                        // Store in AgentContext so it persists through Temporal workflow async boundaries
                        // Activity.Current uses AsyncLocal which doesn't work reliably in Temporal workflows
                        AgentContext.CurrentTraceActivity = activity;
                        
                        // Activity is already started and set as Current by StartActivity
                        Console.WriteLine($"[OpenTelemetry] Successfully restored trace context from {source}:");
                        Console.WriteLine($"  - Original TraceId: {traceId}");
                        Console.WriteLine($"  - Original SpanId (parent): {spanId}");
                        Console.WriteLine($"  - Restored Activity SpanId (new): {activity.SpanId}");
                        Console.WriteLine($"  - Activity.Current is NOW set to restored activity");
                        Console.WriteLine($"  - Activity stored in AgentContext for Temporal workflow persistence");
                        Console.WriteLine($"  - All subsequent operations will be children of this restored context");
                        
                        // Verify Activity.Current was actually set
                        var afterActivity = System.Diagnostics.Activity.Current;
                        if (afterActivity == activity)
                        {
                            Console.WriteLine($"  - ✓ VERIFIED: Activity.Current correctly set to restored activity");
                        }
                        else if (afterActivity != null)
                        {
                            Console.WriteLine($"  - ✗ WARNING: Activity.Current is different activity! Current={afterActivity.SpanId}");
                        }
                        else
                        {
                            Console.WriteLine($"  - ✗ ERROR: Activity.Current is NULL after restoration!");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[OpenTelemetry] WARNING: Failed to create restore context activity - may be sampled out");
                    }
                }
                else
                {
                    Console.WriteLine($"[OpenTelemetry] ERROR: Invalid traceparent format: {traceParent} (expected 4 parts, got {parts.Length})");
                }
            }
            else
            {
                Console.WriteLine("[OpenTelemetry] WARNING: No traceparent found in workflow memo or signal payload");
                var memo = Temporalio.Workflows.Workflow.Memo;
                if (memo == null)
                {
                    Console.WriteLine("[OpenTelemetry] Workflow memo is null");
                }
                else
                {
                    Console.WriteLine($"[OpenTelemetry] Memo keys: {string.Join(", ", memo.Keys)}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the workflow
            Console.WriteLine($"[OpenTelemetry] ERROR: Failed to restore trace context: {ex.Message}");
            Console.WriteLine($"[OpenTelemetry] Stack trace: {ex.StackTrace}");
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
        
        // Get the current activity context (should be set by RestoreTraceContextFromMemo)
        // This ensures we link to the parent trace from the server request
        var currentActivity = System.Diagnostics.Activity.Current;
        var parentContext = currentActivity?.Context ?? default;
        
        if (currentActivity != null)
        {
            Console.WriteLine($"[OpenTelemetry] Creating Temporal operation '{operationName}' as child of trace {currentActivity.TraceId} (span {currentActivity.SpanId})");
        }
        else
        {
            Console.WriteLine($"[OpenTelemetry] WARNING: Creating Temporal operation '{operationName}' as root span - no parent activity found");
        }
        
        var activity = TemporalActivitySource.StartActivity(
            operationName,
            System.Diagnostics.ActivityKind.Internal,
            parentContext: parentContext);
        
        if (activity == null)
        {
            Console.WriteLine($"[OpenTelemetry] WARNING: Failed to create activity for '{operationName}' - may be sampled out");
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
        
        Console.WriteLine($"[OpenTelemetry] Created Temporal activity '{operationName}' with TraceId={activity.TraceId}, SpanId={activity.SpanId}");
        
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
            
            Console.WriteLine($"[OpenTelemetry] DIAGNOSTIC: Recorded {tokens} tokens usage with tags: {string.Join(", ", tagList.Select(t => $"{t.Key}={t.Value}"))}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenTelemetry] ERROR recording token usage: {ex.Message}");
        }
    }
}


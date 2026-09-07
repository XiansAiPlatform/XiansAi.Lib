using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Temporalio.Extensions.OpenTelemetry;

namespace Xians.Lib.Observability;

/// <summary>
/// Turns on OpenTelemetry for agents when the right environment variables are set.
/// Set OpenTelemetry__Enabled=true and OpenTelemetry__OtlpEndpoint to enable.
/// Safe to call when telemetry is off or misconfigured — the agent still starts.
/// </summary>
public static class OpenTelemetryBootstrap
{
    private static readonly object Gate = new();
    private static TracerProvider? _tracerProvider;
    private static bool _initialized;

    /// <summary>
    /// True after telemetry started successfully.
    /// </summary>
    public static bool IsInitialized
    {
        get { lock (Gate) return _initialized; }
    }

    /// <summary>
    /// Collector URL used for export, or null if telemetry is not running.
    /// </summary>
    public static string? OtlpEndpoint { get; private set; }

    /// <summary>
    /// Starts the tracer if enabled. Returns false when disabled, missing config, or on error.
    /// Does not throw — failures are logged and the agent continues without export.
    /// </summary>
    public static bool TryInitialize()
    {
        lock (Gate)
        {
            if (_initialized)
                return true;

            try
            {
                var enabled = IsTruthy(Environment.GetEnvironmentVariable("OpenTelemetry__Enabled"));
                if (!enabled)
                    return false;

                var endpoint = Environment.GetEnvironmentVariable("OpenTelemetry__OtlpEndpoint");
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    Console.Error.WriteLine("[OpenTelemetry] Enabled but OpenTelemetry__OtlpEndpoint is empty — skipping.");
                    return false;
                }

                var serviceName = Environment.GetEnvironmentVariable("OpenTelemetry__ServiceName");
                if (string.IsNullOrWhiteSpace(serviceName))
                    serviceName = "Xians.Agent";

                // Export Temporal workflow/activity spans to the collector over OTLP/gRPC.
                _tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: serviceName, serviceInstanceId: Environment.MachineName))
                    .AddSource(
                        TracingInterceptor.ClientSource.Name,
                        TracingInterceptor.WorkflowsSource.Name,
                        TracingInterceptor.ActivitiesSource.Name)
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(endpoint);
                        o.Protocol = OtlpExportProtocol.Grpc;
                    })
                    .Build();

                OtlpEndpoint = endpoint;
                _initialized = true;
                Console.WriteLine($"[OpenTelemetry] Initialized for {serviceName} → {endpoint}");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[OpenTelemetry] Init failed — continuing without export: {ex.Message}");
                _tracerProvider = null;
                OtlpEndpoint = null;
                _initialized = false;
                return false;
            }
        }
    }

    /// <summary>
    /// Stops telemetry and frees resources. Safe to call more than once.
    /// </summary>
    public static void Shutdown()
    {
        lock (Gate)
        {
            try { _tracerProvider?.Dispose(); }
            catch { /* best-effort cleanup */ }
            _tracerProvider = null;
            OtlpEndpoint = null;
            _initialized = false;
        }
    }

    private static bool IsTruthy(string? value) =>
        bool.TryParse(value, out var b) && b;
}

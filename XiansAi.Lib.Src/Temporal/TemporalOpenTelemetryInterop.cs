using System.Reflection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Temporal;

/// <summary>
/// Optional OpenTelemetry glue for Temporal that does NOT add any compile-time dependency on
/// Temporalio.Extensions.OpenTelemetry. If that package is present at runtime, we enable
/// Temporal's TracingInterceptor for client + worker to produce workflow/activity spans.
/// </summary>
internal static class TemporalOpenTelemetryInterop
{
    private const string TracingInterceptorTypeName =
        "Temporalio.Extensions.OpenTelemetry.TracingInterceptor, Temporalio.Extensions.OpenTelemetry";

    private static bool DebugEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("OTEL_TEMPORAL_DEBUG"), "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("OTEL_TEMPORAL_DEBUG"), "true", StringComparison.OrdinalIgnoreCase);

    public static void TryEnableClientTracing(TemporalClientConnectOptions options)
    {
        TryAttachInterceptor(options);
    }

    public static void TryEnableWorkerTracing(TemporalWorkerOptions options)
    {
        TryAttachInterceptor(options);
    }

    private static void TryAttachInterceptor(object options)
    {
        try
        {
            var interceptorType = Type.GetType(TracingInterceptorTypeName, throwOnError: false);
            if (interceptorType == null)
            {
                if (DebugEnabled)
                {
                    Console.WriteLine($"[OTEL][Temporal] TracingInterceptor not found. Ensure package 'Temporalio.Extensions.OpenTelemetry' is referenced by the host app. ({TracingInterceptorTypeName})");
                }
                return;
            }

            object? interceptor;
            try
            {
                // Temporal 1.7+ requires TracingInterceptorOptions
                var optionsType = Type.GetType(
                    "Temporalio.Extensions.OpenTelemetry.TracingInterceptorOptions, Temporalio.Extensions.OpenTelemetry",
                    throwOnError: false);

                if (optionsType != null)
                {
                    // Try public parameterless ctor first, then allow non-public (best-effort).
                    var interceptorOptions =
                        Activator.CreateInstance(optionsType)
                        ?? Activator.CreateInstance(optionsType, nonPublic: true);

                    interceptor = Activator.CreateInstance(interceptorType, interceptorOptions);
                }
                else
                {
                    // Older versions had a parameterless ctor (or we might not have options type for some reason).
                    interceptor = Activator.CreateInstance(interceptorType);
                }
            }
            catch
            {
                // Fallback: try parameterless ctor in case of API differences.
                interceptor = Activator.CreateInstance(interceptorType);
            }

            if (interceptor == null)
            {
                if (DebugEnabled)
                {
                    Console.WriteLine("[OTEL][Temporal] Failed to create TracingInterceptor instance.");
                }
                return;
            }

            var interceptorsProp = options.GetType().GetProperty("Interceptors", BindingFlags.Public | BindingFlags.Instance);
            if (interceptorsProp == null || !interceptorsProp.CanWrite)
            {
                if (DebugEnabled)
                {
                    Console.WriteLine($"[OTEL][Temporal] Options type '{options.GetType().FullName}' has no writable 'Interceptors' property.");
                }
                return;
            }

            // Infer the interceptor element type from the property itself to avoid Temporal API/version coupling.
            var propType = interceptorsProp.PropertyType;
            var elementType = GetInterceptorElementType(propType);
            if (elementType == null)
            {
                if (DebugEnabled)
                {
                    Console.WriteLine($"[OTEL][Temporal] Unable to infer interceptor element type from Interceptors property type '{propType.FullName}'.");
                }
                return;
            }

            // If interceptor isn't assignable to the element type, we can't attach it.
            if (!elementType.IsAssignableFrom(interceptorType))
            {
                if (DebugEnabled)
                {
                    Console.WriteLine($"[OTEL][Temporal] TracingInterceptor type '{interceptorType.FullName}' is not assignable to Interceptors element type '{elementType.FullName}'.");
                    Console.WriteLine("[OTEL][Temporal] Interceptor implements:");
                    foreach (var itf in interceptorType.GetInterfaces())
                    {
                        Console.WriteLine($"[OTEL][Temporal]   {itf.FullName}");
                    }
                }
                return;
            }

            // Preserve any existing interceptors and append ours (best effort).
            var currentValue = interceptorsProp.GetValue(options);
            var currentList = new List<object>();
            if (currentValue is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        currentList.Add(item);
                    }
                }
            }
            currentList.Add(interceptor);

            var arr = Array.CreateInstance(elementType, currentList.Count);
            for (var i = 0; i < currentList.Count; i++)
            {
                arr.SetValue(currentList[i], i);
            }
            interceptorsProp.SetValue(options, arr);

            if (DebugEnabled)
            {
                Console.WriteLine($"[OTEL][Temporal] Enabled TracingInterceptor on {options.GetType().Name} (Interceptors element type: {elementType.Name}).");
            }
        }
        catch (Exception ex)
        {
            // Best-effort: telemetry must not break Temporal connectivity
            if (DebugEnabled)
            {
                Console.WriteLine($"[OTEL][Temporal] Failed to attach TracingInterceptor: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                try
                {
                    var interceptorType = Type.GetType(TracingInterceptorTypeName, throwOnError: false);
                    if (interceptorType != null)
                    {
                        Console.WriteLine("[OTEL][Temporal] TracingInterceptor constructors:");
                        foreach (var ctor in interceptorType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                        {
                            var ps = ctor.GetParameters();
                            var parts = new List<string>(ps.Length);
                            foreach (var p in ps)
                            {
                                parts.Add($"{p.ParameterType.FullName} {p.Name}");
                            }
                            var sig = string.Join(", ", parts);
                            Console.WriteLine($"[OTEL][Temporal]   .ctor({sig})");
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    private static Type? GetInterceptorElementType(Type propType)
    {
        // Array case: IWorkerInterceptor[] / IClientInterceptor[]
        if (propType.IsArray)
        {
            return propType.GetElementType();
        }

        // Generic enumerable-like case: IReadOnlyCollection<T> / IEnumerable<T>
        if (propType.IsGenericType)
        {
            var args = propType.GetGenericArguments();
            if (args.Length == 1)
            {
                return args[0];
            }
        }

        return null;
    }
}



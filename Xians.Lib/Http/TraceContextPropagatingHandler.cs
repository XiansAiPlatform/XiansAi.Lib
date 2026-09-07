using System.Diagnostics;

namespace Xians.Lib.Http;

/// <summary>
/// Adds tracing headers to outbound HTTP calls so the server can continue
/// the same request chain. Uses the active Activity when one exists.
/// </summary>
internal sealed class TraceContextPropagatingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            // Copy W3C trace headers (traceparent / tracestate) onto the request.
            DistributedContextPropagator.Current.Inject(
                activity,
                request,
                static (carrier, name, value) =>
                {
                    if (carrier is not HttpRequestMessage message || string.IsNullOrEmpty(name) || value is null)
                        return;

                    // Do not overwrite a header the caller already set.
                    if (!message.Headers.Contains(name))
                        message.Headers.TryAddWithoutValidation(name, value);
                });
        }

        return base.SendAsync(request, cancellationToken);
    }
}

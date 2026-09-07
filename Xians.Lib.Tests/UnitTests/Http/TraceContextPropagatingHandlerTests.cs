using System.Diagnostics;
using System.Net;
using Xians.Lib.Http;

namespace Xians.Lib.Tests.UnitTests.Http;

public class TraceContextPropagatingHandlerTests
{
    [Fact]
    public async Task SendAsync_InjectsTraceparent_WhenActivityCurrentExists()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("test");
        using var activity = source.StartActivity("parent");
        Assert.NotNull(activity);

        HttpRequestMessage? captured = null;
        var inner = new CaptureHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var handler = new TraceContextPropagatingHandler { InnerHandler = inner };
        using var client = new HttpClient(handler);

        await client.GetAsync("http://example.invalid/api/agent/logs");

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.Contains("traceparent"));
        var values = captured.Headers.GetValues("traceparent").ToList();
        Assert.Single(values);
        Assert.Contains(activity.TraceId.ToString(), values[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_DoesNotRequireActivity()
    {
        var inner = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new TraceContextPropagatingHandler { InnerHandler = inner };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://example.invalid/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }
}

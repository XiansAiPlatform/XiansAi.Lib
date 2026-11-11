using Microsoft.Extensions.Logging;
using Temporalio.Api.Common.V1;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Logging;
using Xians.Lib.Temporal;

namespace Xians.Lib.Tests.UnitTests.Logging;

/// <summary>
/// Logs written while a message is handled carry its request ID, so all the logs of one delivery
/// can be found together across the workflows and activities it reaches.
/// </summary>
[Collection(LoggerStaticStateCollection.Name)] // Reads API_LOG_LEVEL and the global log queue
public class RequestIdLoggingTests : IDisposable
{
    public RequestIdLoggingTests()
    {
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "DEBUG");
    }

    public void Dispose()
    {
        XiansContext.ClearRequestId();
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, null);
    }

    [Fact]
    public void ALogWrittenWhileAMessageIsHandledCarriesItsRequestId()
    {
        XiansContext.SetRequestId("req-123");

        var log = WriteAndFind("handled message log");

        Assert.NotNull(log.Properties);
        Assert.Equal("req-123", log.Properties![WorkflowConstants.Keys.RequestId]);
    }

    [Fact]
    public void ALogWrittenOutsideMessageHandlingHasNoProperties()
    {
        XiansContext.ClearRequestId();

        var log = WriteAndFind("unrelated log");

        Assert.Null(log.Properties);
    }

    [Fact]
    public void SafeRequestIdIsNullOutsideAnyContext()
    {
        XiansContext.ClearRequestId();

        Assert.Null(XiansContext.SafeRequestId);
    }

    [Fact]
    public void AWorkflowStartedWhileAMessageIsHandledGetsTheRequestIdInItsMemo()
    {
        XiansContext.SetRequestId("req-456");

        var memo = SubWorkflowService.BuildInheritedMemo("tenant", "agent", systemScoped: false, childIdPostfix: null);

        Assert.Equal("req-456", memo[WorkflowConstants.Keys.RequestId]);
    }

    [Fact]
    public void AWorkflowStartedOutsideMessageHandlingHasNoRequestIdInItsMemo()
    {
        XiansContext.ClearRequestId();

        var memo = SubWorkflowService.BuildInheritedMemo("tenant", "agent", systemScoped: false, childIdPostfix: null);

        Assert.False(memo.ContainsKey(WorkflowConstants.Keys.RequestId));
    }

    [Fact]
    public void AnActivityScheduledWithARequestIdGetsTheHeaderAndKeepsOtherHeaders()
    {
        var existing = new Dictionary<string, Payload> { ["other"] = new Payload() };

        var headers = RequestIdInterceptor.WithRequestId(existing, "req-789", Encode);

        Assert.NotNull(headers);
        Assert.True(headers!.ContainsKey("other"));
        Assert.Equal("req-789", headers[RequestIdInterceptor.HeaderKey].Data.ToStringUtf8());
        Assert.False(existing.ContainsKey(RequestIdInterceptor.HeaderKey)); // the caller's headers are not changed
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AnActivityScheduledWithoutARequestIdKeepsItsHeadersAsTheyAre(string? requestId)
    {
        var existing = new Dictionary<string, Payload> { ["other"] = new Payload() };

        Assert.Same(existing, RequestIdInterceptor.WithRequestId(existing, requestId, Encode));
        Assert.Null(RequestIdInterceptor.WithRequestId(null, requestId, Encode));
    }

    private static Payload Encode(string value) => new() { Data = Google.Protobuf.ByteString.CopyFromUtf8(value) };

    [Fact]
    public void AQueuedMessageLogCarriesItsRequestIdOnlyInsideTheScope()
    {
        XiansContext.SetRequestId("outer");

        using (XiansContext.BeginRequestIdScope("queued"))
        {
            Assert.Equal("queued", WriteAndFind("dequeued").Properties![WorkflowConstants.Keys.RequestId]);
        }

        Assert.Equal("outer", XiansContext.GetRequestId());
    }

    [Fact]
    public void AScopeWithoutARequestIdKeepsTheCurrentOne()
    {
        using (XiansContext.BeginRequestIdScope(null))
        {
            Assert.Null(WriteAndFind("no request").Properties);
        }

        Assert.Null(XiansContext.GetRequestId());
    }

    /// <summary>Builds the log row the API logger would send for a message.</summary>
    private static Xians.Lib.Logging.Models.Log WriteAndFind(string text) =>
        ApiLogger.CreateLog(LogLevel.Warning, text, exception: null);
}

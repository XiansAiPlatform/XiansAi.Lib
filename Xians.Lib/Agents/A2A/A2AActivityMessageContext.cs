using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Messaging.Models;

namespace Xians.Lib.Agents.A2A;

/// <summary>
/// A2A message context for use in activity context.
/// Captures responses instead of sending them via HTTP.
/// </summary>
internal class A2AActivityMessageContext : UserMessageContext
{
    private readonly A2AResponseCapture _responseCapture;

    public A2AActivityMessageContext(
        HttpClient httpClient,
        UserMessage message,
        string participantId,
        string requestId,
        string? scope,
        string? hint,
        object? data,
        string tenantId,
        string workflowId,
        string workflowType,
        string? authorization,
        string? threadId,
        A2AResponseCapture responseCapture)
        : base(message, participantId, requestId, scope ?? string.Empty, hint ?? string.Empty, data ?? new object(), tenantId, authorization, threadId)
    {
        _responseCapture = responseCapture ?? throw new ArgumentNullException(nameof(responseCapture));
    }

    /// <summary>
    /// Captures the response instead of sending it via HTTP.
    /// </summary>
    public override Task ReplyAsync(string response)
    {
        _responseCapture.HasResponse = true;
        _responseCapture.Text = response;
        _responseCapture.Data = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Captures the response with data instead of sending it via HTTP.
    /// </summary>
    public override Task ReplyWithDataAsync(string content, object? data)
    {
        _responseCapture.HasResponse = true;
        _responseCapture.Text = content;
        _responseCapture.Data = data;
        return Task.CompletedTask;
    }
}


using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Workflows.Messaging;
using Xians.Lib.Workflows.Messaging.Models;
using System.Net;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Context provided to webhook handlers.
/// Contains webhook-specific information and response handling.
/// For agent-wide operations (Knowledge, Documents, Schedules), use XiansContext.CurrentAgent or XiansContext.CurrentWorkflow.
/// 
/// Access webhook properties (ParticipantId, Name, Payload, etc.) via the Webhook property.
/// Set the response via the Response property.
/// </summary>
public class WebhookContext
{
    private readonly Dictionary<string, string>? _metadata;
    private readonly ILogger<WebhookContext> _logger;

    /// <summary>
    /// Gets the current webhook with name, payload, and context information.
    /// Use this to access ParticipantId, Scope, Name, Payload, Authorization, RequestId, etc.
    /// </summary>
    public virtual WebhookMessage Webhook { get; protected set; }

    /// <summary>
    /// Gets or sets the response to send back for the webhook.
    /// Set this property to define the HTTP-style response.
    /// </summary>
    public virtual WebhookResponse Response { get; set; } = new WebhookResponse();

    /// <summary>
    /// Gets the optional metadata for the webhook.
    /// </summary>
    public Dictionary<string, string>? Metadata => _metadata;

    internal WebhookContext(
        string participantId,
        string? scope,
        string name,
        object? payload,
        string? authorization,
        string requestId,
        string tenantId,
        Dictionary<string, string>? metadata = null)
    {
        _metadata = metadata;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<WebhookContext>();

        // Initialize webhook message with context
        Webhook = new WebhookMessage(participantId, scope, name, payload, authorization, requestId, tenantId);

        // Initialize default response
        Response = new WebhookResponse
        {
            StatusCode = HttpStatusCode.OK,
            ContentType = "application/json"
        };
    }

    /// <summary>
    /// Sets a successful JSON response with the specified content.
    /// Shorthand for setting Response property.
    /// </summary>
    /// <param name="content">The JSON content string.</param>
    public virtual void Respond(string content)
    {
        Response = new WebhookResponse
        {
            StatusCode = HttpStatusCode.OK,
            Content = content,
            ContentType = "application/json"
        };

        _logger.LogDebug(
            "Webhook response set: RequestId={RequestId}, StatusCode={StatusCode}",
            Webhook.RequestId,
            Response.StatusCode);
    }

    /// <summary>
    /// Sets a successful JSON response with the specified data object.
    /// The object will be serialized to JSON.
    /// Shorthand for setting Response property.
    /// </summary>
    /// <param name="data">The data object to serialize as JSON.</param>
    public virtual void Respond(object data)
    {
        Response = WebhookResponse.Ok(data);

        _logger.LogDebug(
            "Webhook response set: RequestId={RequestId}, StatusCode={StatusCode}",
            Webhook.RequestId,
            Response.StatusCode);
    }

    /// <summary>
    /// Sets the webhook response.
    /// Shorthand for setting Response property.
    /// </summary>
    /// <param name="response">The webhook response to set.</param>
    public virtual void Respond(WebhookResponse response)
    {
        Response = response;

        _logger.LogDebug(
            "Webhook response set: RequestId={RequestId}, StatusCode={StatusCode}",
            Webhook.RequestId,
            Response.StatusCode);
    }
}

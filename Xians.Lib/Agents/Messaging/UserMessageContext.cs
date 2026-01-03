using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Agents.Core;
using Xians.Lib.Workflows.Messaging.Models;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Context provided to user message handlers.
/// Contains message-specific information and messaging operations.
/// For agent-wide operations (Knowledge, Documents, Schedules), use XiansContext.CurrentAgent or XiansContext.CurrentWorkflow.
/// All operations are workflow-aware and handle both workflow and activity contexts.
/// </summary>
public class UserMessageContext
{
    private readonly string _participantId;
    private readonly string _requestId;
    private readonly string? _scope;
    private readonly string? _hint;
    private readonly string? _authorization;
    private readonly string? _threadId;
    private readonly object? _data;
    private readonly string _tenantId;
    private readonly Dictionary<string, string>? _metadata;

    /// <summary>
    /// Gets the user message.
    /// </summary>
    public UserMessage Message { get; private set; }

    /// <summary>
    /// Gets the participant ID (user ID).
    /// </summary>
    public string ParticipantId => _participantId;

    /// <summary>
    /// Gets the request ID for tracking.
    /// </summary>
    public virtual string RequestId => _requestId;

    /// <summary>
    /// Gets the scope of the message.
    /// </summary>
    public virtual string? Scope => _scope;

    /// <summary>
    /// Gets the hint for message processing.
    /// </summary>
    public string? Hint => _hint;

    /// <summary>
    /// Gets the thread ID for conversation tracking.
    /// </summary>
    public string? ThreadId => _threadId;

    /// <summary>
    /// Gets the tenant ID for this workflow instance.
    /// For system-scoped agents, this indicates which tenant initiated the workflow.
    /// For non-system-scoped agents, this is always the agent's registered tenant.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when tenant ID is not properly initialized.</exception>
    public virtual string TenantId 
    { 
        get 
        {
            if (string.IsNullOrEmpty(_tenantId))
            {
                throw new InvalidOperationException(
                    "Tenant ID is not available. UserMessageContext was not properly initialized.");
            }
            return _tenantId;
        }
    }

    /// <summary>
    /// Gets the data object associated with the message.
    /// </summary>
    public object? Data => _data;

    /// <summary>
    /// Gets the authorization token for the message, if provided.
    /// </summary>
    public string? Authorization => _authorization;

    /// <summary>
    /// Gets the optional metadata for the message.
    /// </summary>
    public Dictionary<string, string>? Metadata => _metadata;

    /// <summary>
    /// Gets the messaging operations collection for replying and accessing chat history.
    /// For agent-wide operations, use XiansContext.CurrentAgent (Knowledge, Documents) or XiansContext.CurrentWorkflow (Schedules).
    /// </summary>
    public virtual MessageCollection Messages { get; protected set; }

    internal UserMessageContext(UserMessage message)
    {
        Message = message;
        _participantId = string.Empty;
        _requestId = string.Empty;
        _scope = string.Empty;
        _hint = string.Empty;
        _data = new object();
        _tenantId = string.Empty;

        // Initialize messaging collection
        Messages = new MessageCollection(
            _participantId, _requestId, _scope, _hint, _data, _tenantId, _authorization, _threadId);
    }

    internal UserMessageContext(
        UserMessage message, 
        string participantId, 
        string requestId, 
        string? scope,
        string? hint,
        object? data,
        string tenantId,
        string? authorization = null,
        string? threadId = null,
        Dictionary<string, string>? metadata = null)
    {
        Message = message;
        _participantId = participantId;
        _requestId = requestId;
        _scope = scope;
        _hint = hint;
        _data = data;
        _tenantId = tenantId;
        _authorization = authorization;
        _threadId = threadId;
        _metadata = metadata;

        // Initialize messaging collection with context
        Messages = new MessageCollection(
            participantId, requestId, scope, hint, data, tenantId, authorization, threadId);
    }
}


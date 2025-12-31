using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Documents.Models;
using Xians.Lib.Workflows.Knowledge;
using Xians.Lib.Workflows.Knowledge.Models;
using Xians.Lib.Workflows.Messaging;
using Xians.Lib.Workflows.Messaging.Models;
using Xians.Lib.Workflows.Documents;
using Xians.Lib.Workflows.Documents.Models;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Context provided to user message handlers.
/// Contains message information and methods to reply.
/// </summary>
public class UserMessageContext
{
    private readonly string _participantId;
    private readonly string _requestId;
    private readonly string _scope;
    private readonly string _hint;
    private readonly string? _authorization;
    private readonly string? _threadId;
    private readonly object _data;
    private readonly string _tenantId;

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
    public virtual string Scope => _scope;

    /// <summary>
    /// Gets the hint for message processing.
    /// </summary>
    public string Hint => _hint;

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
    public object Data => _data;

    internal UserMessageContext(UserMessage message)
    {
        Message = message;
        _participantId = string.Empty;
        _requestId = string.Empty;
        _scope = string.Empty;
        _hint = string.Empty;
        _data = new object();
        _tenantId = string.Empty;
    }

    internal UserMessageContext(
        UserMessage message, 
        string participantId, 
        string requestId, 
        string scope,
        string hint,
        object data,
        string tenantId,
        string? authorization = null,
        string? threadId = null)
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
    }

    /// <summary>
    /// Sends a reply to the user (synchronous wrapper).
    /// Note: Prefer using ReplyAsync in async contexts.
    /// </summary>
    /// <param name="response">The response object to send.</param>
    public virtual async Task ReplyAsync(string response)
    {
        await SendMessageToUserAsync(response, null);
    }

    /// <summary>
    /// Sends a reply with both text and data to the user.
    /// </summary>
    /// <param name="content">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public virtual async Task ReplyWithDataAsync(string content, object? data)
    {
        await SendMessageToUserAsync(content, data);
    }

    /// <summary>
    /// Retrieves paginated chat history for this conversation from the server.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="page">The page number to retrieve (default: 1).</param>
    /// <param name="pageSize">The number of messages per page (default: 10).</param>
    /// <returns>A list of DbMessage objects representing the chat history.</returns>
    public virtual async Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
    {
        var logger = GetLogger();
        var workflowType = GetWorkflowType();
        
        logger.LogInformation(
            "Fetching chat history: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}, Page={Page}, PageSize={PageSize}, Tenant={Tenant}",
            workflowType,
            _participantId,
            page,
            pageSize,
            TenantId);

        var request = new GetMessageHistoryRequest
        {
            WorkflowType = workflowType,
            ParticipantId = _participantId,
            Scope = _scope,
            TenantId = TenantId,
            Page = page,
            PageSize = pageSize
        };

        List<DbMessage> messages;
        
        // If in workflow, execute as activity for determinism
        // If in activity, execute directly
        // Note: Filtering is done in MessageService for consistency across all paths
        if (Workflow.InWorkflow)
        {
            messages = await Workflow.ExecuteActivityAsync(
                (MessageActivities act) => act.GetMessageHistoryAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            // Direct execution when not in workflow
            var activity = GetMessageActivity();
            messages = await activity.GetMessageHistoryAsync(request);
        }

        logger.LogInformation(
            "Chat history retrieved: {Count} messages, Tenant={Tenant}",
            messages.Count,
            TenantId);

        return messages;
    }

    /// <summary>
    /// Retrieves the last hint for this conversation from the server.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <returns>The last hint string, or null if not found.</returns>
    public virtual async Task<string?> GetLastHintAsync()
    {
        var logger = GetLogger();
        var workflowType = GetWorkflowType();
        
        logger.LogInformation(
            "Fetching last hint: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}, Tenant={Tenant}",
            workflowType,
            _participantId,
            TenantId);

        var request = new GetLastHintRequest
        {
            WorkflowType = workflowType,
            ParticipantId = _participantId,
            Scope = _scope,
            TenantId = TenantId
        };

        string? hint;
        
        // If in workflow, execute as activity for determinism
        // If in activity, execute directly
        if (Workflow.InWorkflow)
        {
            hint = await Workflow.ExecuteActivityAsync(
                (MessageActivities act) => act.GetLastHintAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            // Direct execution when not in workflow
            var activity = GetMessageActivity();
            hint = await activity.GetLastHintAsync(request);
        }

        logger.LogInformation(
            "Last hint retrieved: Found={Found}, Tenant={Tenant}",
            hint != null,
            TenantId);

        return hint;
    }

    /// <summary>
    /// Retrieves knowledge by name from the platform.
    /// Automatically uses the current tenant and agent context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to fetch.</param>
    /// <returns>The knowledge object, or null if not found.</returns>
    public virtual async Task<Xians.Lib.Agents.Knowledge.Models.Knowledge?> GetKnowledgeAsync(string knowledgeName)
    {
        var logger = GetLogger();
        var workflowType = GetWorkflowType();
        
        logger.LogInformation(
            "Fetching knowledge: Name={Name}, WorkflowType={WorkflowType}, Tenant={Tenant}",
            knowledgeName,
            workflowType,
            TenantId);

        var request = new GetKnowledgeRequest
        {
            KnowledgeName = knowledgeName,
            AgentName = GetAgentNameFromWorkflow(),
            TenantId = TenantId
        };

        Xians.Lib.Agents.Knowledge.Models.Knowledge? knowledge;
        
        // If in workflow, execute as activity for determinism
        // If in activity (e.g., from A2A call in tool), execute directly
        if (Workflow.InWorkflow)
        {
            knowledge = await Workflow.ExecuteActivityAsync(
                (KnowledgeActivities act) => act.GetKnowledgeAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            // Direct execution when not in workflow (e.g., called from activity via A2A)
            var activity = GetKnowledgeActivity();
            knowledge = await activity.GetKnowledgeAsync(request);
        }

        logger.LogInformation(
            "Knowledge fetch completed: Name={Name}, Found={Found}",
            knowledgeName,
            knowledge != null);

        return knowledge;
    }

    /// <summary>
    /// Updates or creates knowledge in the platform.
    /// Automatically uses the current tenant and agent context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge.</param>
    /// <param name="content">The knowledge content.</param>
    /// <param name="type">Optional knowledge type (e.g., "instruction", "document").</param>
    /// <returns>True if successful, false otherwise.</returns>
    public virtual async Task<bool> UpdateKnowledgeAsync(
        string knowledgeName,
        string content,
        string? type = null)
    {
        var logger = GetLogger();
        var workflowType = GetWorkflowType();
        
        logger.LogInformation(
            "Updating knowledge: Name={Name}, WorkflowType={WorkflowType}, Type={Type}, Tenant={Tenant}",
            knowledgeName,
            workflowType,
            type,
            TenantId);

        var request = new UpdateKnowledgeRequest
        {
            KnowledgeName = knowledgeName,
            Content = content,
            Type = type,
            AgentName = GetAgentNameFromWorkflow(),
            TenantId = TenantId
        };

        bool result;
        
        if (Workflow.InWorkflow)
        {
            result = await Workflow.ExecuteActivityAsync(
                (KnowledgeActivities act) => act.UpdateKnowledgeAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            var activity = GetKnowledgeActivity();
            result = await activity.UpdateKnowledgeAsync(request);
        }

        logger.LogInformation(
            "Knowledge update completed: Name={Name}, Success={Success}",
            knowledgeName,
            result);

        return result;
    }

    /// <summary>
    /// Deletes knowledge from the platform.
    /// Automatically uses the current tenant and agent context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to delete.</param>
    /// <returns>True if deleted, false if not found.</returns>
    public virtual async Task<bool> DeleteKnowledgeAsync(string knowledgeName)
    {
        var logger = GetLogger();
        var workflowType = GetWorkflowType();
        
        logger.LogInformation(
            "Deleting knowledge: Name={Name}, WorkflowType={WorkflowType}, Tenant={Tenant}",
            knowledgeName,
            workflowType,
            TenantId);

        var request = new DeleteKnowledgeRequest
        {
            KnowledgeName = knowledgeName,
            AgentName = GetAgentNameFromWorkflow(),
            TenantId = TenantId
        };

        bool result;
        
        if (Workflow.InWorkflow)
        {
            result = await Workflow.ExecuteActivityAsync(
                (KnowledgeActivities act) => act.DeleteKnowledgeAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            var activity = GetKnowledgeActivity();
            result = await activity.DeleteKnowledgeAsync(request);
        }

        logger.LogInformation(
            "Knowledge delete completed: Name={Name}, Success={Success}",
            knowledgeName,
            result);

        return result;
    }

    /// <summary>
    /// Lists all knowledge for this agent.
    /// Automatically uses the current tenant and agent context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <returns>A list of knowledge items.</returns>
    public virtual async Task<List<Xians.Lib.Agents.Knowledge.Models.Knowledge>> ListKnowledgeAsync()
    {
        var logger = GetLogger();
        var workflowType = GetWorkflowType();
        
        logger.LogInformation(
            "Listing knowledge: WorkflowType={WorkflowType}, Tenant={Tenant}",
            workflowType,
            TenantId);

        var request = new ListKnowledgeRequest
        {
            AgentName = GetAgentNameFromWorkflow(),
            TenantId = TenantId
        };

        List<Xians.Lib.Agents.Knowledge.Models.Knowledge> knowledgeList;
        
        if (Workflow.InWorkflow)
        {
            knowledgeList = await Workflow.ExecuteActivityAsync(
                (KnowledgeActivities act) => act.ListKnowledgeAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            var activity = GetKnowledgeActivity();
            knowledgeList = await activity.ListKnowledgeAsync(request);
        }

        logger.LogInformation(
            "Knowledge list completed: Count={Count}",
            knowledgeList.Count);

        return knowledgeList;
    }

    #region Document Operations

    /// <summary>
    /// Saves a document to the database.
    /// Automatically uses the current tenant context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <param name="options">Optional storage options (TTL, overwrite, etc.).</param>
    /// <returns>The saved document with its assigned ID.</returns>
    public virtual async Task<Document> SaveDocumentAsync(Document document, DocumentOptions? options = null)
    {
        var logger = GetLogger();
        
        logger.LogInformation(
            "Saving document: Tenant={Tenant}",
            TenantId);

        var request = new SaveDocumentRequest
        {
            Document = document,
            Options = options,
            TenantId = TenantId
        };

        Document savedDocument;
        
        if (Workflow.InWorkflow)
        {
            savedDocument = await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.SaveDocumentAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            var activity = GetDocumentActivity();
            savedDocument = await activity.SaveDocumentAsync(request);
        }

        logger.LogInformation(
            "Document saved: Id={Id}",
            savedDocument.Id);

        return savedDocument;
    }

    /// <summary>
    /// Retrieves a document by its ID.
    /// Automatically uses the current tenant context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <returns>The document if found, null otherwise.</returns>
    public virtual async Task<Document?> GetDocumentAsync(string id)
    {
        var logger = GetLogger();
        
        logger.LogInformation(
            "Getting document: Id={Id}, Tenant={Tenant}",
            id,
            TenantId);

        var request = new GetDocumentRequest
        {
            Id = id,
            TenantId = TenantId
        };

        Document? document;
        
        if (Workflow.InWorkflow)
        {
            document = await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.GetDocumentAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            var activity = GetDocumentActivity();
            document = await activity.GetDocumentAsync(request);
        }

        logger.LogInformation(
            "Document get completed: Found={Found}",
            document != null);

        return document;
    }

    /// <summary>
    /// Queries documents based on filters.
    /// Automatically uses the current tenant context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="query">The query parameters.</param>
    /// <returns>A list of matching documents.</returns>
    public virtual async Task<List<Document>> QueryDocumentsAsync(DocumentQuery query)
    {
        var logger = GetLogger();
        
        logger.LogInformation(
            "Querying documents: Type={Type}, Limit={Limit}, Tenant={Tenant}",
            query.Type,
            query.Limit,
            TenantId);

        var request = new QueryDocumentsRequest
        {
            Query = query,
            TenantId = TenantId
        };

        List<Document> documents;
        
        if (Workflow.InWorkflow)
        {
            documents = await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.QueryDocumentsAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            var activity = GetDocumentActivity();
            documents = await activity.QueryDocumentsAsync(request);
        }

        logger.LogInformation(
            "Query completed: Count={Count}",
            documents.Count);

        return documents;
    }

    /// <summary>
    /// Updates an existing document.
    /// Automatically uses the current tenant context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="document">The document to update (must have an ID).</param>
    /// <returns>True if updated, false if not found.</returns>
    public virtual async Task<bool> UpdateDocumentAsync(Document document)
    {
        var logger = GetLogger();
        
        logger.LogInformation(
            "Updating document: Id={Id}, Tenant={Tenant}",
            document.Id,
            TenantId);

        var request = new UpdateDocumentRequest
        {
            Document = document,
            TenantId = TenantId
        };

        bool result;
        
        if (Workflow.InWorkflow)
        {
            result = await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.UpdateDocumentAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            var activity = GetDocumentActivity();
            result = await activity.UpdateDocumentAsync(request);
        }

        logger.LogInformation(
            "Update completed: Success={Success}",
            result);

        return result;
    }

    /// <summary>
    /// Deletes a document by its ID.
    /// Automatically uses the current tenant context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="id">The document ID to delete.</param>
    /// <returns>True if deleted, false if not found.</returns>
    public virtual async Task<bool> DeleteDocumentAsync(string id)
    {
        var logger = GetLogger();
        
        logger.LogInformation(
            "Deleting document: Id={Id}, Tenant={Tenant}",
            id,
            TenantId);

        var request = new DeleteDocumentRequest
        {
            Id = id,
            TenantId = TenantId
        };

        bool result;
        
        if (Workflow.InWorkflow)
        {
            result = await Workflow.ExecuteActivityAsync(
                (DocumentActivities act) => act.DeleteDocumentAsync(request),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        MaximumAttempts = 3,
                        InitialInterval = TimeSpan.FromSeconds(1),
                        MaximumInterval = TimeSpan.FromSeconds(10),
                        BackoffCoefficient = 2
                    }
                });
        }
        else
        {
            var activity = GetDocumentActivity();
            result = await activity.DeleteDocumentAsync(request);
        }

        logger.LogInformation(
            "Delete completed: Success={Success}",
            result);

        return result;
    }

    #endregion

    /// <summary>
    /// Extracts agent name from workflow type.
    /// Workflow type format: "AgentName:WorkflowName"
    /// </summary>
    private string GetAgentNameFromWorkflow()
    {
        var workflowType = GetWorkflowType();
        var separatorIndex = workflowType.IndexOf(':');
        
        if (separatorIndex > 0)
        {
            return workflowType.Substring(0, separatorIndex);
        }
        
        // Fallback: use entire workflow type as agent name
        return workflowType;
    }

    /// <summary>
    /// Gets the workflow type from current context (workflow or activity).
    /// Can be overridden in derived classes (e.g., A2AMessageContext).
    /// </summary>
    protected virtual string GetWorkflowType()
    {
        if (Workflow.InWorkflow)
        {
            return Workflow.Info.WorkflowType;
        }
        else if (ActivityExecutionContext.HasCurrent)
        {
            return ActivityExecutionContext.Current.Info.WorkflowType;
        }
        else
        {
            throw new InvalidOperationException("Not in workflow or activity context");
        }
    }

    /// <summary>
    /// Gets an appropriate logger for the current context.
    /// </summary>
    protected virtual ILogger GetLogger()
    {
        return XiansLogger.GetLogger<UserMessageContext>();
    }

    /// <summary>
    /// Gets a KnowledgeActivities instance for direct execution.
    /// Used when not in workflow context.
    /// </summary>
    private KnowledgeActivities GetKnowledgeActivity()
    {
        // Get HTTP client from the current agent
        var agent = XiansContext.CurrentAgent;
        if (agent.HttpService == null)
        {
            throw new InvalidOperationException("HTTP service not available for knowledge operations");
        }
        
        return new KnowledgeActivities(agent.HttpService.Client, agent.CacheService);
    }

    /// <summary>
    /// Gets a MessageActivities instance for direct execution.
    /// Used when not in workflow context.
    /// </summary>
    private MessageActivities GetMessageActivity()
    {
        var agent = XiansContext.CurrentAgent;
        if (agent.HttpService == null)
        {
            throw new InvalidOperationException("HTTP service not available for message operations");
        }
        
        return new MessageActivities(agent.HttpService.Client);
    }

    /// <summary>
    /// Gets a DocumentActivities instance for direct execution.
    /// Used when not in workflow context.
    /// </summary>
    private DocumentActivities GetDocumentActivity()
    {
        var agent = XiansContext.CurrentAgent;
        if (agent.HttpService == null)
        {
            throw new InvalidOperationException("HTTP service not available for document operations");
        }
        
        return new DocumentActivities(agent.HttpService.Client);
    }

    /// <summary>
    /// Internal method to send messages back to the user via Temporal activity.
    /// Uses Workflow.ExecuteActivityAsync to ensure proper determinism and retry handling.
    /// Exceptions bubble up to be handled by the workflow's top-level event loop.
    /// </summary>
    private async Task SendMessageToUserAsync(string content, object? data)
    {
        Workflow.Logger.LogDebug(
            "Preparing to send message: ParticipantId={ParticipantId}, RequestId={RequestId}, ContentLength={ContentLength}, Tenant={Tenant}",
            _participantId,
            _requestId,
            content?.Length ?? 0,
            TenantId);
        
        var request = new SendMessageRequest
        {
            ParticipantId = _participantId,
            WorkflowId = Workflow.Info.WorkflowId,
            WorkflowType = Workflow.Info.WorkflowType,
            Text = content,
            Data = data ?? _data, // Use provided data or original message data
            RequestId = _requestId,
            Scope = _scope,
            ThreadId = _threadId,
            Authorization = _authorization,
            Hint = _hint, // Pass through the hint from the original message
            Origin = null,
            Type = "Chat",
            TenantId = TenantId  // Pass tenant context for system-scoped agents
        };

        Workflow.Logger.LogDebug(
            "Executing SendMessage activity: WorkflowId={WorkflowId}, WorkflowType={WorkflowType}, Tenant={Tenant}, Endpoint=api/agent/conversation/outbound/chat",
            request.WorkflowId,
            request.WorkflowType,
            TenantId);

        // Execute as Temporal activity for proper determinism, retries, and observability
        await Workflow.ExecuteActivityAsync(
            (MessageActivities act) => act.SendMessageAsync(request),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
                RetryPolicy = new()
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(1),
                    MaximumInterval = TimeSpan.FromSeconds(10),
                    BackoffCoefficient = 2
                }
            });
        
        Workflow.Logger.LogDebug(
            "Message sent successfully: ParticipantId={ParticipantId}, RequestId={RequestId}",
            _participantId,
            _requestId);
    }
}


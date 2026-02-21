using System.Reflection;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Common.MultiTenancy;

using Xians.Lib.Agents.A2A;
using Xians.Lib.Agents.Core.Registry;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Central context hub for accessing all Xians SDK functionality.
/// Provides unified access to agents, workflows, messaging, knowledge, documents, and schedules.
/// </summary>
/// <remarks>
/// This class acts as a facade over separate registries for better separation of concerns.
/// The underlying registries can be accessed via dependency injection for testability.
/// </remarks>
public static class XiansContext
{
    // Extracted registries for better separation of concerns
    // Made internal static for backward compatibility, but accessible for DI scenarios
    internal static readonly IAgentRegistry _agentRegistry = new AgentRegistry();
    internal static readonly IWorkflowRegistry _workflowRegistry = new WorkflowRegistry();

    // AsyncLocal storage for participant ID - isolated per async execution context
    private static readonly AsyncLocal<string?> _asyncLocalParticipantId = new AsyncLocal<string?>();
    
    // AsyncLocal storage for authorization - isolated per async execution context
    private static readonly AsyncLocal<string?> _asyncLocalAuthorization = new AsyncLocal<string?>();
    
    // AsyncLocal storage for request ID - isolated per async execution context
    private static readonly AsyncLocal<string?> _asyncLocalRequestId = new AsyncLocal<string?>();
    
    // AsyncLocal storage for tenant ID - isolated per async execution context
    private static readonly AsyncLocal<string?> _asyncLocalTenantId = new AsyncLocal<string?>();

    // AsyncLocal storage for idPostfix - isolated per async execution context (for tests/out-of-workflow)
    private static readonly AsyncLocal<string?> _asyncLocalIdPostfix = new AsyncLocal<string?>();

    // AsyncLocal storage for current agent override - for unit tests without Temporal workflow context.
    // When set, CurrentAgent returns this agent instead of resolving from workflow type.
    private static readonly AsyncLocal<XiansAgent?> _asyncLocalCurrentAgentOverride = new AsyncLocal<XiansAgent?>();

    // Static fallback for Local mode: when an agent is registered with LocalMode=true, it is stored here.
    // AsyncLocal does not flow from fixture InitializeAsync to test execution context, so tests need this
    // static fallback to resolve CurrentAgent without Temporal workflow context.
    private static XiansAgent? _staticCurrentAgentOverride;

    #region Workflow/Activity Context

    /// <summary>
    /// Gets the current workflow ID from Temporal context.
    /// Internal - for public access, use XiansContext.CurrentWorkflow.WorkflowId
    /// </summary>
    public static string WorkflowId => GetWorkflowId();

    /// <summary>
    /// Gets the current tenant ID from async execution context, workflow metadata, or workflow ID.
    /// Tries in order: async local context → workflow search attributes/memo → workflow ID extraction → certificate (non-system-scoped agents).
    /// This is global context information that applies to the entire workflow execution.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context or tenant ID cannot be extracted.</exception>
    public static string TenantId => GetTenantId();

    /// <summary>
    /// Gets the current certificate user ID from the authorization context.
    /// Reads the certificate from the current authorization token and extracts the user ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when authorization is not available or certificate is invalid.</exception>
    public static string CertificateUser => GetCertificateUser();

    /// <summary>
    /// Gets the current workflow type from Temporal context.
    /// Internal - for public access, use XiansContext.CurrentWorkflow.WorkflowType
    /// </summary>
    internal static string WorkflowType => GetWorkflowType();

    /// <summary>
    /// Gets the current agent name extracted from the workflow type.
    /// Internal - for public access, use XiansContext.CurrentAgent.Name
    /// </summary>
    internal static string AgentName
    {
        get
        {
            var workflowType = WorkflowType;
            var separatorIndex = workflowType.IndexOf(':');

            if (separatorIndex > 0)
            {
                return workflowType.Substring(0, separatorIndex);
            }

            // Fallback: use entire workflow type as agent name
            return workflowType;
        }
    }

    /// <summary>
    /// Checks if the code is currently executing within a Temporal workflow.
    /// </summary>
    public static bool InWorkflow => Workflow.InWorkflow;

    /// <summary>
    /// Checks if the code is currently executing within a Temporal activity.
    /// </summary>
    public static bool InActivity => ActivityExecutionContext.HasCurrent;

    /// <summary>
    /// Checks if the code is currently executing within a Temporal workflow or activity.
    /// </summary>
    public static bool InWorkflowOrActivity => Workflow.InWorkflow || ActivityExecutionContext.HasCurrent;

    /// <summary>
    /// Safely gets the current workflow ID without throwing exceptions.
    /// Returns null if not in workflow or activity context.
    /// Use this in logging and other scenarios where exceptions are not desired.
    /// </summary>
    public static string? SafeWorkflowId => TryGetWorkflowId();

    /// <summary>
    /// Safely gets the current workflow run ID without throwing exceptions.
    /// Returns null if not in workflow or activity context.
    /// Use this in logging and other scenarios where exceptions are not desired.
    /// </summary>
    public static string? SafeWorkflowRunId => TryGetWorkflowRunId();

    /// <summary>
    /// Safely gets the current workflow type without throwing exceptions.
    /// Returns null if not in workflow or activity context.
    /// Use this in logging and other scenarios where exceptions are not desired.
    /// </summary>
    public static string? SafeWorkflowType => TryGetWorkflowType();

    /// <summary>
    /// Safely gets the current agent name without throwing exceptions.
    /// Returns null if not in workflow or activity context.
    /// Use this in logging and other scenarios where exceptions are not desired.
    /// </summary>
    public static string? SafeAgentName => TryGetAgentName();

    /// <summary>
    /// Safely gets the current tenant ID without throwing exceptions.
    /// Returns null if not in workflow or activity context.
    /// Use this in logging and other scenarios where exceptions are not desired.
    /// </summary>
    public static string? SafeTenantId => TryGetTenantId();

    /// <summary>
    /// Safely gets the current participant ID without throwing exceptions.
    /// Returns null if not in workflow or activity context.
    /// Use this in logging and other scenarios where exceptions are not desired.
    /// </summary>
    public static string? SafeParticipantId => TryGetParticipantId();

    /// <summary>
    /// Safely gets the current idPostfix without throwing exceptions.
    /// Returns null if not in workflow or activity context.
    /// Use this in logging and other scenarios where exceptions are not desired.
    /// </summary>
    public static string? SafeIdPostfix => TryGetIdPostfix();

    /// <summary>
    /// Safely gets the current certificate user ID without throwing exceptions.
    /// Returns null if authorization is not available or certificate is invalid.
    /// Use this in logging and other scenarios where exceptions are not desired.
    /// </summary>
    public static string? SafeCertificateUser => TryGetCertificateUser();

    #endregion

    #region Workflow/Activity Context Helper Methods

    private const string NotInContextErrorMessage = 
        "Not in workflow or activity context. This operation requires Temporal context.";

    /// <summary>
    /// Sets the participant ID for the current async execution context.
    /// This value is isolated per thread/async flow and won't affect other concurrent operations.
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    /// <param name="participantId">The participant ID to set for this execution context.</param>
    internal static void SetParticipantId(string participantId)
    {
        _asyncLocalParticipantId.Value = participantId;
    }

    /// <summary>
    /// Clears the participant ID from the current async execution context.
    /// Call this in a finally block to clean up after activity execution.
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    internal static void ClearParticipantId()
    {
        _asyncLocalParticipantId.Value = null;
    }

    /// <summary>
    /// Sets the authorization for the current async execution context.
    /// This value is isolated per thread/async flow and won't affect other concurrent operations.
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    /// <param name="authorization">The authorization token to set for this execution context.</param>
    internal static void SetAuthorization(string? authorization)
    {
        _asyncLocalAuthorization.Value = authorization;
    }

    /// <summary>
    /// Clears the authorization from the current async execution context.
    /// Call this in a finally block to clean up after activity execution.
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    internal static void ClearAuthorization()
    {
        _asyncLocalAuthorization.Value = null;
    }

    /// <summary>
    /// Sets the request ID for the current async execution context.
    /// This value is isolated per thread/async flow and won't affect other concurrent operations.
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    /// <param name="requestId">The request ID to set for this execution context.</param>
    internal static void SetRequestId(string requestId)
    {
        _asyncLocalRequestId.Value = requestId;
    }

    /// <summary>
    /// Clears the request ID from the current async execution context.
    /// Call this in a finally block to clean up after activity execution.
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    internal static void ClearRequestId()
    {
        _asyncLocalRequestId.Value = null;
    }

    /// <summary>
    /// Sets the tenant ID for the current async execution context.
    /// This value is isolated per thread/async flow and won't affect other concurrent operations.
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    /// <param name="tenantId">The tenant ID to set for this execution context.</param>
    internal static void SetTenantId(string tenantId)
    {
        _asyncLocalTenantId.Value = tenantId;
    }

    /// <summary>
    /// Clears the tenant ID from the current async execution context.
    /// Call this in a finally block to clean up after activity execution.
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    internal static void ClearTenantId()
    {
        _asyncLocalTenantId.Value = null;
    }

    /// <summary>
    /// Sets the idPostfix for the current async execution context.
    /// Use when calling schedule/document/knowledge APIs from outside workflow context (e.g. tests).
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    internal static void SetIdPostfix(string idPostfix)
    {
        _asyncLocalIdPostfix.Value = idPostfix;
    }

    /// <summary>
    /// Clears the idPostfix from the current async execution context.
    /// Internal - accessible only from tests via InternalsVisibleTo.
    /// </summary>
    internal static void ClearIdPostfix()
    {
        _asyncLocalIdPostfix.Value = null;
    }

    /// <summary>
    /// Gets the authorization from the current async execution context.
    /// </summary>
    /// <returns>The authorization token if set, otherwise null.</returns>
    public static string? GetAuthorization()
    {
        return _asyncLocalAuthorization.Value;
    }

    /// <summary>
    /// Gets the request ID from the current async execution context.
    /// </summary>
    /// <returns>The request ID if set, otherwise null.</returns>
    public static string? GetRequestId()
    {
        return _asyncLocalRequestId.Value;
    }

    /// <summary>
    /// Gets the tenant ID from the current async execution context, workflow metadata, or workflow ID.
    /// Tries in order: async local context → workflow search attributes/memo → workflow ID extraction →
    /// certificate tenant (when agent is not system-scoped).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context or tenant ID cannot be extracted.</exception>
    public static string GetTenantId()
    {
        // First check if it was set via SetTenantId() in the current async context
        if (!string.IsNullOrEmpty(_asyncLocalTenantId.Value))
        {
            return _asyncLocalTenantId.Value;
        }

        // Try workflow metadata (search attributes → memo)
        var fromWorkflowMetadata = GetWorkflowMetadata(Common.WorkflowConstants.Keys.TenantId);
        if (!string.IsNullOrEmpty(fromWorkflowMetadata))
        {
            return fromWorkflowMetadata;
        }

        // Fall back to extracting from workflow ID
        try
        {
            return TenantContext.ExtractTenantId(WorkflowId);
        }
        catch (Exception ex)
        {
            // Last resort: if agent is not system-scoped, use tenant from certificate
            try
            {
                var agent = CurrentAgent;
                if (!agent.SystemScoped && !string.IsNullOrWhiteSpace(agent.Options?.CertificateTenantId))
                {
                    return agent.Options!.CertificateTenantId;
                }
            }
            catch { /* ignore - will rethrow below */ }

            var workflowIdForError = SafeWorkflowId ?? "(not in workflow/activity context)";
            throw new InvalidOperationException(
                $"Failed to extract tenant ID from workflow ID '{workflowIdForError}'. " +
                $"Ensure workflow ID follows the expected format. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the participant ID from the current async execution context, search attributes, memo, or workflow ID.
    /// Tries in order: async local context → search attributes → memo → workflow ID parsing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetParticipantId()
    {
        // First check if it was set via SetParticipantId() in the current async context
        if (!string.IsNullOrEmpty(_asyncLocalParticipantId.Value))
        {
            return _asyncLocalParticipantId.Value;
        }
        
        // Fall back to existing logic (search attributes, memo, workflow ID)
        return GetWorkflowMetadata(Common.WorkflowConstants.Keys.UserId) 
            ?? throw new InvalidOperationException(NotInContextErrorMessage + "Workflow UserId not found. Provide a participant ID explicitly.");
    }

    /// <summary>
    /// Gets the idPostfix from async context, search attributes, memo, or workflow ID.
    /// Tries in order: async local (SetIdPostfix) → search attributes → memo → workflow ID parsing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetIdPostfix()
    {
        if (!string.IsNullOrEmpty(_asyncLocalIdPostfix.Value))
            return _asyncLocalIdPostfix.Value;
        return GetWorkflowMetadata(Common.WorkflowConstants.Keys.idPostfix) ?? throw new InvalidOperationException(NotInContextErrorMessage + "Workflow idPostfix not found. Provide a idPostfix explicitly.");
    }

    /// <summary>
    /// Generic method to retrieve workflow metadata from search attributes, memo, or workflow ID.
    /// Tries in order: search attributes → memo → workflow ID parsing (for idPostfix only).
    /// </summary>
    private static string? GetWorkflowMetadata(string keyName)
    {
        var fromContext = WorkflowMetadataResolver.GetFromWorkflowContext(keyName);
        if (!string.IsNullOrEmpty(fromContext))
            return fromContext;

        if (keyName == Common.WorkflowConstants.Keys.idPostfix)
            return WorkflowMetadataResolver.ParseIdPostfixFromWorkflowId(WorkflowMetadataResolver.GetWorkflowId());

        return null;
    }

    /// <summary>
    /// Gets the current workflow ID.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    private static string GetWorkflowId() => WorkflowMetadataResolver.GetWorkflowId();

    /// <summary>
    /// Gets the current workflow type.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    private static string GetWorkflowType() => 
        GetFromContext(
            () => Workflow.Info.WorkflowType,
            () => ActivityExecutionContext.Current.Info.WorkflowType
        );

    /// <summary>
    /// Gets the current workflow run ID.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    private static string GetWorkflowRunId() => WorkflowMetadataResolver.GetWorkflowRunId();

    /// <summary>
    /// Gets the current task queue name.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetTaskQueue() => 
        GetFromContext(
            () => Workflow.Info.TaskQueue,
            () => ActivityExecutionContext.Current.Info.TaskQueue
        );

    /// <summary>
    /// Generic helper to get data from either workflow or activity context.
    /// </summary>
    private static T GetFromContext<T>(Func<T> fromWorkflow, Func<T> fromActivity)
    {
        if (Workflow.InWorkflow)
            return fromWorkflow();
        
        if (ActivityExecutionContext.HasCurrent)
            return fromActivity();
        
        throw new InvalidOperationException(NotInContextErrorMessage);
    }

    /// <summary>
    /// Tries to get a value from context without throwing exceptions.
    /// </summary>
    private static T? TryGetFromContext<T>(Func<T> getter) where T : class
    {
        try
        {
            return InWorkflowOrActivity ? getter() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to get the current workflow ID without throwing an exception.
    /// </summary>
    /// <returns>The workflow ID if in workflow/activity context, otherwise null.</returns>
    private static string? TryGetWorkflowId() => TryGetFromContext(GetWorkflowId);

    /// <summary>
    /// Tries to get the current workflow type without throwing an exception.
    /// </summary>
    /// <returns>The workflow type if in workflow/activity context, otherwise null.</returns>
    private static string? TryGetWorkflowType() => TryGetFromContext(GetWorkflowType);

    /// <summary>
    /// Tries to get the current workflow run ID without throwing an exception.
    /// </summary>
    /// <returns>The workflow run ID if in workflow/activity context, otherwise null.</returns>
    private static string? TryGetWorkflowRunId() => TryGetFromContext(GetWorkflowRunId);

    /// <summary>
    /// Tries to get the current agent name without throwing an exception.
    /// </summary>
    /// <returns>The agent name if in workflow/activity context, otherwise null.</returns>
    private static string? TryGetAgentName() => TryGetFromContext(() => AgentName);

    /// <summary>
    /// Tries to get the current tenant ID without throwing an exception.
    /// Checks async local context first, then falls back to workflow ID extraction.
    /// </summary>
    /// <returns>The tenant ID if available, otherwise null.</returns>
    private static string? TryGetTenantId()
    {
        try
        {
            return GetTenantId();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to get the current participant ID without throwing an exception.
    /// </summary>
    /// <returns>The participant ID if in workflow/activity context, otherwise null.</returns>
    internal static string? TryGetParticipantId()
    {
        try
        {
            return InWorkflowOrActivity ? GetParticipantId() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to get the current idPostfix without throwing an exception.
    /// Checks async local (SetIdPostfix) first, then workflow context.
    /// </summary>
    /// <returns>The idPostfix if available, otherwise null.</returns>
    internal static string? TryGetIdPostfix()
    {
        if (!string.IsNullOrEmpty(_asyncLocalIdPostfix.Value))
            return _asyncLocalIdPostfix.Value;
        try
        {
            return GetIdPostfix();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to get idPostfix, preferring the parent workflow's description when in activity context.
    /// When in activity and a Temporal client is provided, fetches the workflow description via DescribeAsync
    /// and extracts idPostfix from TypedSearchAttributes or Memo (more reliable than parsing workflow ID
    /// which can be polluted by Temporal's timestamp suffix on scheduled workflows).
    /// </summary>
    /// <param name="client">Temporal client. When null or not in activity, falls back to sync resolution (search attrs, memo, workflow ID parsing).</param>
    /// <returns>The idPostfix if found, otherwise null.</returns>
    public static async Task<string?> TryGetIdPostfixAsync(ITemporalClient? client = null) =>
        await WorkflowMetadataResolver.ResolveIdPostfixAsync(client);

    /// <summary>
    /// Gets the current certificate user ID from the platform initialization.
    /// Accesses the certificate information that was parsed during platform startup.
    /// </summary>
    /// <returns>The user ID extracted from the certificate during platform initialization.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in agent context or certificate information is not available.</exception>
    private static string GetCertificateUser()
    {
        try
        {
            var userId = CurrentAgent.Options?.CertificateInfo?.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException("Certificate user ID is not available. Ensure the platform was properly initialized with a valid certificate.");
            }
            return userId;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to access certificate user from agent options. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Tries to get the current certificate user ID without throwing an exception.
    /// </summary>
    /// <returns>The certificate user ID if available, otherwise null.</returns>
    private static string? TryGetCertificateUser()
    {
        try
        {
            return CurrentAgent.Options?.CertificateInfo.UserId;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Current Agent Access

    /// <summary>
    /// Gets the current agent instance based on the workflow context.
    /// Use this to access agent-level operations like Knowledge and Documents.
    /// When <see cref="SetCurrentAgentForTests"/> has been called (e.g. in unit tests), returns that agent instead,
    /// enabling use of a mock agent with Local Knowledge provider outside Temporal workflow context.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow/activity context or agent not found.</exception>
    public static XiansAgent CurrentAgent
    {
        get
        {
            var overrideAgent = _asyncLocalCurrentAgentOverride.Value;
            if (overrideAgent != null)
            {
                return overrideAgent;
            }

            // When not in workflow/activity (e.g. unit tests), AsyncLocal may be empty due to different
            // execution context. Use static fallback set when agent is registered with LocalMode=true.
            if (!InWorkflowOrActivity)
            {
                var staticOverride = _staticCurrentAgentOverride;
                if (staticOverride != null)
                {
                    return staticOverride;
                }
                throw new InvalidOperationException(
                    "Not in workflow or activity context. This operation requires Temporal context. " +
                    "For unit tests, initialize with XiansPlatform.InitializeForTestsAsync() and register an agent.");
            }

            var agentName = AgentName;
            if (_agentRegistry.TryGet(agentName, out var agent))
            {
                return agent!;
            }

            throw new InvalidOperationException(
                $"Agent '{agentName}' not found in registry. Ensure the agent is registered with XiansPlatform.");
        }
    }

    /// <summary>
    /// Gets the current workflow instance based on the workflow context.
    /// Use this to access workflow-level operations like Schedules.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow/activity context or workflow not found.</exception>
    public static XiansWorkflow CurrentWorkflow
    {
        get
        {
            if (!InWorkflow && !InActivity)
            {
                throw new InvalidOperationException(
                    "Not in workflow or activity context. CurrentWorkflow is only available within Temporal workflows or activities.");
            }

            var workflowType = WorkflowType;
            
            if (_workflowRegistry.TryGet(workflowType, out var workflow))
            {
                return workflow!;
            }

            throw new InvalidOperationException(
                $"Workflow '{workflowType}' not found in registry. " +
                $"Ensure the workflow has been registered and is running.");
        }
    }

    #endregion

    #region Operation Helpers

    private static readonly Lazy<WorkflowHelper> _workflowHelper = new();
    private static readonly Lazy<MessagingHelper> _messagingHelper = new();

    /// <summary>
    /// Gets workflow operations for starting, executing, signaling, and querying workflows.
    /// Also provides access to the Temporal client for advanced operations.
    /// </summary>
    public static WorkflowHelper Workflows => _workflowHelper.Value;

    /// <summary>
    /// Gets messaging operations for proactive messaging and A2A communication.
    /// </summary>
    public static MessagingHelper Messaging => _messagingHelper.Value;

    /// <summary>
    /// Gets metrics operations for reporting usage statistics.
    /// Automatically handles workflow vs non-workflow contexts.
    /// Can be used directly without Track() - all fields auto-populate from XiansContext.
    /// </summary>
    /// <example>
    /// <code>
    /// // Direct usage without Track()
    /// await XiansContext.Metrics
    ///     .ForModel("gpt-4")
    ///     .WithMetric("tokens", "total", 150, "tokens")
    ///     .ReportAsync();
    /// </code>
    /// </example>
    public static Metrics.MetricsCollection Metrics => CurrentAgent.Metrics;

    #endregion

    #region Agent Registry Access

    /// <summary>
    /// Gets a registered agent by name.
    /// </summary>
    /// <param name="agentName">The name of the agent to retrieve.</param>
    /// <returns>The agent instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when agentName is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the agent is not found.</exception>
    public static XiansAgent GetAgent(string agentName)
    {
        return _agentRegistry.Get(agentName);
    }

    /// <summary>
    /// Tries to get a registered agent by name.
    /// </summary>
    /// <param name="agentName">The name of the agent to retrieve.</param>
    /// <param name="agent">The agent instance if found, null otherwise.</param>
    /// <returns>True if the agent was found, false otherwise.</returns>
    public static bool TryGetAgent(string agentName, out XiansAgent? agent)
    {
        return _agentRegistry.TryGet(agentName, out agent);
    }

    /// <summary>
    /// Gets all registered agents.
    /// </summary>
    /// <returns>Enumerable of all registered agent instances.</returns>
    public static IEnumerable<XiansAgent> GetAllAgents()
    {
        return _agentRegistry.GetAll();
    }

    #endregion

    #region Workflow Registry Access

    /// <summary>
    /// Gets a registered workflow by workflow type.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier (format: "AgentName:WorkflowName").</param>
    /// <returns>The workflow instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowType is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the workflow is not found.</exception>
    public static XiansWorkflow GetWorkflow(string workflowType)
    {
        return _workflowRegistry.Get(workflowType);
    }

    /// <summary>
    /// Gets a built-in workflow by name for the current agent.
    /// Automatically constructs the workflow type using the current agent's name and built-in workflow conventions.
    /// </summary>
    /// <param name="workflowName">The built-in workflow name (e.g., "Conversational", "WebWorkflow").</param>
    /// <returns>The workflow instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowName is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the workflow is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when not in agent context.</exception>
    public static XiansWorkflow GetBuiltInWorkflow(string workflowName)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
        {
            throw new ArgumentNullException(nameof(workflowName), "Workflow name cannot be null or empty.");
        }

        var workflowType = GetBuiltInWorkflowType(workflowName);

        if (_workflowRegistry.TryGet(workflowType, out var workflow))
        {
            return workflow!;
        }

        throw new KeyNotFoundException(
            $"Built-in workflow '{workflowName}' not found for agent '{CurrentAgent.Name}'. " +
            $"Expected workflow type: '{workflowType}'. " +
            $"Available workflows: {string.Join(", ", _workflowRegistry.GetAll().Select(w => w.WorkflowType))}");
    }

    /// <summary>
    /// Tries to get a built-in workflow by name for the current agent.
    /// Automatically constructs the workflow type using the current agent's name and built-in workflow conventions.
    /// </summary>
    /// <param name="workflowName">The built-in workflow name.</param>
    /// <param name="workflow">The workflow instance if found, null otherwise.</param>
    /// <returns>True if the workflow was found, false otherwise.</returns>
    public static bool TryGetBuiltInWorkflow(string workflowName, out XiansWorkflow? workflow)
    {
        workflow = null;

        if (string.IsNullOrWhiteSpace(workflowName))
            return false;

        try
        {
            var workflowType = GetBuiltInWorkflowType(workflowName);
            return _workflowRegistry.TryGet(workflowType, out workflow);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Constructs the workflow type for a built-in workflow.
    /// </summary>
    private static string GetBuiltInWorkflowType(string workflowName)
    {
        var agent = CurrentAgent ?? throw new InvalidOperationException(
            "Cannot get built-in workflow: No agent context available. " +
            "Ensure you're calling this from within a workflow or after registering an agent.");

        return BuildBuiltInWorkflowType(agent.Name, workflowName);
    }

    /// <summary>
    /// Tries to get a registered workflow by workflow type.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier.</param>
    /// <param name="workflow">The workflow instance if found, null otherwise.</param>
    /// <returns>True if the workflow was found, false otherwise.</returns>
    public static bool TryGetWorkflow(string workflowType, out XiansWorkflow? workflow)
    {
        return _workflowRegistry.TryGet(workflowType, out workflow);
    }

    /// <summary>
    /// Gets all registered workflows.
    /// </summary>
    /// <returns>Enumerable of all registered workflow instances.</returns>
    public static IEnumerable<XiansWorkflow> GetAllWorkflows()
    {
        return _workflowRegistry.GetAll();
    }

    #endregion

    #region A2A Operations

    private static readonly Lazy<A2AContextOperations> _a2aOperations = new(() => new A2AContextOperations());

    /// <summary>
    /// Gets the A2A (Agent-to-Agent) operations for sending messages between workflows.
    /// Provides simplified API for A2A communication without manually creating A2AClient instances.
    /// </summary>
    /// <example>
    /// <code>
    /// // Send chat to a built-in workflow by name
    /// var response = await XiansContext.A2A.SendChatToBuiltInAsync("WebWorkflow", new A2AMessage { Text = "Fetch data" });
    /// 
    /// // Send simple text message
    /// var response = await XiansContext.A2A.SendTextAsync("WebWorkflow", "Hello");
    /// 
    /// // Send data to a built-in workflow
    /// var response = await XiansContext.A2A.SendDataToBuiltInAsync("DataWorkflow", new A2AMessage { Data = myData });
    /// </code>
    /// </example>
    public static A2AContextOperations A2A => _a2aOperations.Value;

    #endregion

    #region Workflow Identity Construction

    /// <summary>
    /// Constructs a builtin workflow type identifier.
    /// </summary>
    /// <param name="agentName">The name of the agent owning the workflow.</param>
    /// <param name="workflowName">The name for the builtin workflow.</param>
    /// <returns>A workflow type in the format "{AgentName}:{name}"</returns>
    public static string BuildBuiltInWorkflowType(string agentName, string workflowName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentException("Agent name cannot be null or empty.", nameof(agentName));
        }

        if (string.IsNullOrWhiteSpace(workflowName))
        {
            throw new ArgumentException("Workflow name cannot be null or empty.", nameof(workflowName));
        }

        return agentName + ":" + workflowName;
    }

    /// <summary>
    /// Constructs a builtin workflow ID.
    /// </summary>
    /// <param name="agentName">The name of the agent owning the workflow.</param>
    /// <param name="workflowName">The name for the builtin workflow.</param>
    /// <returns>A workflow ID in the format "{TenantId}:{AgentName}:{name}"</returns>
    public static string BuildBuiltInWorkflowId(string agentName, string workflowName)
    {
        var idPostfix = XiansContext.GetIdPostfix();
        var workflowId = $"{TenantId}:{BuildBuiltInWorkflowType(agentName, workflowName)}:{idPostfix}";
        return workflowId;
    }

    /// <summary>
    /// Gets the workflow type string from a workflow class Type.
    /// Extracts the name from the [Workflow] attribute.
    /// </summary>
    /// <param name="workflowClassType">The Type of the workflow class.</param>
    /// <returns>The workflow type string from the WorkflowAttribute.</returns>
    /// <exception cref="ArgumentNullException">Thrown when workflowClassType is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the workflow class doesn't have a WorkflowAttribute with a Name.</exception>
    public static string GetWorkflowTypeFor(Type workflowClassType)
    {
        if (workflowClassType == null)
        {
            throw new ArgumentNullException(nameof(workflowClassType));
        }

        var workflowAttr = workflowClassType.GetCustomAttribute<WorkflowAttribute>();
        if (workflowAttr?.Name == null)
        {
            throw new InvalidOperationException(
                $"Workflow class '{workflowClassType.Name}' does not have a WorkflowAttribute with a Name property set.");
        }

        return workflowAttr.Name;
    }

    #endregion

    #region Internal Registration

    /// <summary>
    /// Registers an agent in the static registry.
    /// Called internally by XiansPlatform when agents are created.
    /// </summary>
    /// <param name="agent">The agent to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when agent is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an agent with the same name is already registered.</exception>
    internal static void RegisterAgent(XiansAgent agent)
    {
        _agentRegistry.Register(agent);
    }

    /// <summary>
    /// Registers a workflow in the static registry.
    /// Called internally by XiansWorkflow when workflows are started.
    /// </summary>
    /// <param name="workflowType">The workflow type identifier.</param>
    /// <param name="workflow">The workflow instance to register.</param>
    internal static void RegisterWorkflow(string workflowType, XiansWorkflow workflow)
    {
        _workflowRegistry.Register(workflowType, workflow);
    }

    /// <summary>
    /// Clears all in-memory and static state including registries, caches, and handlers.
    /// For testing purposes only.
    /// </summary>
    /// <remarks>
    /// <para><strong>NOTE:</strong> This method only clears LOCAL state (in-memory registries, static handlers, caches).</para>
    /// <para>It does NOT delete agents, knowledge, documents, or workflows from the server.</para>
    /// <para><strong>For real server integration tests:</strong> Use <c>RealServerTestCleanupHelper</c> which handles both
    /// server resource deletion (agents, knowledge, documents, workflows) AND local state cleanup.</para>
    /// <para><strong>For mock server tests:</strong> Use this method directly since no server cleanup is needed.</para>
    /// <para><strong>What gets cleared:</strong></para>
    /// <list type="bullet">
    /// <item><description>Agent and workflow registries</description></item>
    /// <item><description>BuiltinWorkflow message handlers and options</description></item>
    /// <item><description>Knowledge activity static services</description></item>
    /// <item><description>Settings service cache</description></item>
    /// <item><description>Certificate cache</description></item>
    /// </list>
    /// </remarks>
    /// <summary>
    /// Sets the current agent for unit tests.
    /// When called, <see cref="CurrentAgent"/> returns this agent instead of resolving from workflow context.
    /// Use with an agent registered via <see cref="XiansPlatform.InitializeForTestsAsync"/> so it has
    /// Local Knowledge provider. Call <see cref="ClearCurrentAgentForTests"/> in test cleanup.
    /// Sets both AsyncLocal (same async context) and static fallback (for fixture init vs test execution context).
    /// </summary>
    /// <param name="agent">The agent to use as CurrentAgent (e.g. one with LocalMode options and LocalKnowledgeProvider).</param>
    internal static void SetCurrentAgentForTests(XiansAgent agent)
    {
        var a = agent ?? throw new ArgumentNullException(nameof(agent));
        _asyncLocalCurrentAgentOverride.Value = a;
        _staticCurrentAgentOverride = a;
    }

    /// <summary>
    /// Clears the current agent override set by <see cref="SetCurrentAgentForTests"/>.
    /// Call in test cleanup to avoid cross-test contamination.
    /// </summary>
    internal static void ClearCurrentAgentForTests()
    {
        _asyncLocalCurrentAgentOverride.Value = null;
        _staticCurrentAgentOverride = null;
    }

    internal static void CleanupForTests()
    {
        ClearCurrentAgentForTests();
        _agentRegistry.Clear();
        _workflowRegistry.Clear();
        // Clear workflow handlers to prevent test contamination
        Xians.Lib.Temporal.Workflows.BuiltinWorkflow.ClearHandlersForTests();
        // Clear static services in activities to prevent cross-test contamination
        Xians.Lib.Temporal.Workflows.Knowledge.KnowledgeActivities.ClearStaticServicesForTests();
        // Clear cached server settings to prevent cross-test contamination
        Xians.Lib.Common.Infrastructure.SettingsService.ResetCache();
        // Clear certificate cache to prevent stale certificates
        Xians.Lib.Common.Security.CertificateCache.Clear();
    }

    /// <summary>
    /// Clears only the agent and workflow registries.
    /// Intended for testing purposes only.
    /// </summary>
    /// <remarks>
    /// <para>This is a minimal cleanup method that only clears registries.</para>
    /// <para>For comprehensive cleanup, use <see cref="CleanupForTests"/> instead, which also clears
    /// handlers, caches, and other static state.</para>
    /// </remarks>
    internal static void Clear()
    {
        ClearCurrentAgentForTests();
        _agentRegistry.Clear();
        _workflowRegistry.Clear();
    }

    #endregion
}


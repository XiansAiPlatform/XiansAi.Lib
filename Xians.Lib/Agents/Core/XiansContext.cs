using System.Reflection;
using Temporalio.Activities;
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

    #region Workflow/Activity Context

    /// <summary>
    /// Gets the current workflow ID from Temporal context.
    /// Internal - for public access, use XiansContext.CurrentWorkflow.WorkflowId
    /// </summary>
    public static string WorkflowId => GetWorkflowId();

    /// <summary>
    /// Gets the current tenant ID extracted from the workflow ID.
    /// This is global context information that applies to the entire workflow execution.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context or tenant ID cannot be extracted.</exception>
    public static string TenantId
    {
        get
        {
            try
            {
                return TenantContext.ExtractTenantId(WorkflowId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to extract tenant ID from workflow ID '{WorkflowId}'. " +
                    $"Ensure workflow ID follows the expected format. Error: {ex.Message}", ex);
            }
        }
    }

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

    #endregion

    #region Workflow/Activity Context Helper Methods

    private const string NotInContextErrorMessage = 
        "Not in workflow or activity context. This operation requires Temporal context.";

    /// <summary>
    /// Sets the participant ID for the current async execution context.
    /// This value is isolated per thread/async flow and won't affect other concurrent operations.
    /// </summary>
    /// <param name="participantId">The participant ID to set for this execution context.</param>
    public static void SetParticipantId(string participantId)
    {
        _asyncLocalParticipantId.Value = participantId;
    }

    /// <summary>
    /// Clears the participant ID from the current async execution context.
    /// Call this in a finally block to clean up after activity execution.
    /// </summary>
    public static void ClearParticipantId()
    {
        _asyncLocalParticipantId.Value = null;
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
            ?? throw new InvalidOperationException(NotInContextErrorMessage);
    }

    /// <summary>
    /// Gets the idPostfix from search attributes, memo, or workflow ID.
    /// Tries in order: search attributes → memo → workflow ID parsing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public static string GetIdPostfix() => 
        GetWorkflowMetadata(Common.WorkflowConstants.Keys.idPostfix) ?? GetIdPostfixFromWorkflowId() ?? throw new InvalidOperationException(NotInContextErrorMessage);

    /// <summary>
    /// Generic method to retrieve workflow metadata from search attributes, memo, or workflow ID.
    /// Tries in order: search attributes → memo → workflow ID parsing.
    /// </summary>
    private static string? GetWorkflowMetadata(string keyName)
    {
        var fromSearchAttrs = GetFromSearchAttributes(keyName);
        if (!string.IsNullOrEmpty(fromSearchAttrs))
            return fromSearchAttrs;

        var fromMemo = GetFromMemo(keyName);
        if (!string.IsNullOrEmpty(fromMemo))
            return fromMemo;

        return null;
    }

    /// <summary>
    /// Gets the current workflow ID.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    private static string GetWorkflowId() => 
        GetFromContext(
            () => Workflow.Info.WorkflowId,
            () => ActivityExecutionContext.Current.Info.WorkflowId
        );

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
    private static string GetWorkflowRunId() => 
        GetFromContext(
            () => Workflow.Info.RunId,
            () => ActivityExecutionContext.Current.Info.WorkflowRunId
        );

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
    /// </summary>
    /// <returns>The tenant ID if in workflow/activity context, otherwise null.</returns>
    private static string? TryGetTenantId()
    {
        try
        {
            return InWorkflowOrActivity ? TenantId : null;
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
    private static string? TryGetParticipantId()
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
    /// </summary>
    /// <returns>The idPostfix if in workflow/activity context, otherwise null.</returns>
    internal static string? TryGetIdPostfix()
    {
        try
        {
            return InWorkflowOrActivity ? GetIdPostfix() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to get idPostfix from search attributes (workflow context only).
    /// </summary>
    private static string? GetFromSearchAttributes(string keyName)
    {
        try
        {
            if (Workflow.InWorkflow)
            {
                var searchAttrs = Workflow.TypedSearchAttributes;
                var key = Temporalio.Common.SearchAttributeKey.CreateKeyword(keyName);
                return searchAttrs.Get(key);
                
            }
            // Note: Activities don't have direct access to search attributes
        }
        catch
        {
            // Search attribute doesn't exist or wrong type, continue to next method
        }
        return null;
    }

    /// <summary>
    /// Attempts to get idPostfix from workflow memo.
    /// Works in workflow context only (activities inherit parent workflow ID).
    /// </summary>
    private static string? GetFromMemo(string keyName)
    {
        try
        {
            if (Workflow.InWorkflow)
            {
                if (Workflow.Memo.TryGetValue(keyName, out var value))
                {
                    return value.Payload.Data.ToStringUtf8()?.Replace("\"", "");
                }
            }
            // Note: Activities don't have access to memo directly, they use the parent workflow's ID
        }
        catch
        {
            // Memo doesn't exist or can't be parsed, continue to next method
        }
        return null;
    }

    /// <summary>
    /// Parses idPostfix from workflow ID as fallback.
    /// Workflow ID format: {tenantId}:{agentName}:{workflowName}:{idPostfix}
    /// Works in both workflow and activity contexts.
    /// </summary>
    private static string? GetIdPostfixFromWorkflowId()
    {
        try
        {
            var workflowId = GetWorkflowId();
            var parts = workflowId.Split(':');
            if (parts.Length < 4)
            {
                return null;
            }
            return parts[3];
        }
        catch
        {
            // Workflow ID doesn't exist or can't be parsed, continue to next method
        }
        return null;
    }

    #endregion

    #region Current Agent Access

    /// <summary>
    /// Gets the current agent instance based on the workflow context.
    /// Use this to access agent-level operations like Knowledge and Documents.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow/activity context or agent not found.</exception>
    public static XiansAgent CurrentAgent
    {
        get
        {
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
    private static readonly MetricsHelper _metricsHelper = new();


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
    /// </summary>
    public static MetricsHelper Metrics => _metricsHelper;

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
    internal static void CleanupForTests()
    {
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
        _agentRegistry.Clear();
        _workflowRegistry.Clear();
    }

    #endregion
}


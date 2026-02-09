using Microsoft.Extensions.Logging;
using Temporalio.Client.Schedules;
using Temporalio.Common;
using Temporalio.Workflows;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Temporal.Workflows.Scheduling.Models;

namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Fluent builder for creating scheduled workflow executions.
/// </summary>
public class ScheduleBuilder
{
    private readonly string _scheduleName;
    private readonly XiansAgent _agent;
    private readonly ITemporalClientService _temporalService;
    private readonly ILogger<ScheduleBuilder> _logger;

    private ScheduleSpec? _scheduleSpec;
    private object[]? _workflowArgs;
    private Dictionary<string, object>? _workflowMemo;
    private SearchAttributeCollection? _typedSearchAttributes;
    private RetryPolicy? _retryPolicy;
    private string _workflowType;
    private TimeSpan? _timeout;
    private SchedulePolicy? _schedulePolicy;
    private ScheduleState? _scheduleState;

    private readonly string _idPostfix;

    internal ScheduleBuilder(
        string scheduleName,
        XiansAgent agent,
        string workflowType,
        ITemporalClientService temporalService,
        string? idPostfix = null)
    {
        _idPostfix = idPostfix ?? XiansContext.GetIdPostfix();   
        _workflowType = workflowType;
        _scheduleName = scheduleName ?? throw new ArgumentNullException(nameof(scheduleName));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _temporalService = temporalService ?? throw new ArgumentNullException(nameof(temporalService));
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleBuilder>();
    }

    /// <summary>
    /// Sets a cron-based schedule specification using standard 5-field cron format.
    /// Format: [minute] [hour] [day of month] [month] [day of week]
    /// </summary>
    /// <param name="cronExpression">Cron expression (e.g., "0 9 * * *" for daily at 9 AM UTC).</param>
    /// <param name="timezone">Optional IANA timezone (e.g., "America/New_York"). Defaults to UTC if not specified.</param>
    /// <example>
    /// Common patterns:
    /// - "0 9 * * *" = Daily at 9 AM
    /// - "0 9 * * 1-5" = Weekdays at 9 AM
    /// - "*/30 * * * *" = Every 30 minutes
    /// - "0 0 1 * *" = First of month at midnight
    /// </example>
    public ScheduleBuilder WithCronSchedule(string cronExpression, string? timezone = null)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be null or empty", nameof(cronExpression));

        _scheduleSpec = new ScheduleSpec
        {
            CronExpressions = new List<string> { cronExpression },
            TimeZoneName = timezone
        };

        return this;
    }

    /// <summary>
    /// Sets an interval-based schedule specification.
    /// </summary>
    /// <param name="interval">Time interval between executions.</param>
    /// <param name="offset">Optional offset from the start of the interval.</param>
    public ScheduleBuilder WithIntervalSchedule(TimeSpan interval, TimeSpan? offset = null)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Interval must be greater than zero", nameof(interval));

        _scheduleSpec = new ScheduleSpec
        {
            Intervals = new List<ScheduleIntervalSpec>
            {
                offset.HasValue 
                    ? new(Every: interval, Offset: offset.Value)
                    : new(Every: interval)
            }
        };

        return this;
    }

    /// <summary>
    /// Sets a calendar-based schedule for specific date/time.
    /// </summary>
    /// <param name="scheduledTime">Specific date and time to execute.</param>
    /// <param name="timezone">Optional timezone (defaults to UTC).</param>
    public ScheduleBuilder WithCalendarSchedule(DateTime scheduledTime, string? timezone = null)
    {
        _scheduleSpec = new ScheduleSpec
        {
            Calendars = new List<ScheduleCalendarSpec>
            {
                new()
                {
                    Year = new List<ScheduleRange> { new(scheduledTime.Year) },
                    Month = new List<ScheduleRange> { new(scheduledTime.Month) },
                    DayOfMonth = new List<ScheduleRange> { new(scheduledTime.Day) },
                    Hour = new List<ScheduleRange> { new(scheduledTime.Hour) },
                    Minute = new List<ScheduleRange> { new(scheduledTime.Minute) },
                    Second = new List<ScheduleRange> { new(scheduledTime.Second) }
                }
            },
            TimeZoneName = timezone
        };

        return this;
    }

    /// <summary>
    /// Sets a custom schedule specification for advanced scenarios.
    /// </summary>
    /// <param name="spec">Custom schedule specification.</param>
    public ScheduleBuilder WithScheduleSpec(ScheduleSpec spec)
    {
        _scheduleSpec = spec ?? throw new ArgumentNullException(nameof(spec));
        return this;
    }

    /// <summary>
    /// Sets the input arguments that will be passed to each scheduled workflow execution.
    /// These arguments must match the parameters of the workflow's [WorkflowRun] method.
    /// </summary>
    /// <param name="args">Workflow input arguments in the same order as the workflow method parameters.</param>
    /// <example>
    /// For workflow: public async Task RunAsync(string url, int retries)
    /// Use: .WithInput("https://example.com", 3)
    /// </example>
    public ScheduleBuilder WithInput(params object[] args)
    {
        _workflowArgs = args;
        return this;
    }


    /// <summary>
    /// Sets workflow memo for additional metadata.
    /// </summary>
    /// <param name="memo">Workflow memo dictionary.</param>
    public ScheduleBuilder WithMemo(Dictionary<string, object> memo)
    {
        _workflowMemo = memo ?? throw new ArgumentNullException(nameof(memo));
        return this;
    }

    /// <summary>
    /// Sets typed search attributes for the scheduled workflow executions.
    /// Allows the schedule to inherit search attributes from parent workflow.
    /// </summary>
    /// <param name="searchAttributes">Typed search attributes collection.</param>
    public ScheduleBuilder WithTypedSearchAttributes(SearchAttributeCollection? searchAttributes)
    {
        _typedSearchAttributes = searchAttributes;
        return this;
    }

    /// <summary>
    /// Sets a retry policy for the scheduled workflow executions.
    /// </summary>
    /// <param name="retryPolicy">Retry policy configuration.</param>
    public ScheduleBuilder WithRetryPolicy(Temporalio.Common.RetryPolicy retryPolicy)
    {
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        return this;
    }

    /// <summary>
    /// Sets a timeout for the scheduled workflow executions.
    /// </summary>
    /// <param name="timeout">Workflow execution timeout.</param>
    public ScheduleBuilder WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentException("Timeout must be greater than zero", nameof(timeout));

        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the schedule policy (overlap policy, etc.).
    /// </summary>
    /// <param name="policy">Schedule policy configuration.</param>
    public ScheduleBuilder WithSchedulePolicy(SchedulePolicy policy)
    {
        _schedulePolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>
    /// Sets the overlap policy that controls what happens when a new scheduled execution 
    /// is triggered while a previous one is still running.
    /// Alias for WithSchedulePolicy with overlap-specific configuration.
    /// </summary>
    /// <param name="overlapPolicy">The overlap policy to apply.</param>
    public ScheduleBuilder WithOverlapPolicy(Temporalio.Api.Enums.V1.ScheduleOverlapPolicy overlapPolicy)
    {
        _schedulePolicy = new SchedulePolicy
        {
            Overlap = overlapPolicy
        };
        return this;
    }

    /// <summary>
    /// Sets whether the schedule should start paused.
    /// </summary>
    /// <param name="paused">True to start the schedule in paused state.</param>
    /// <param name="note">Optional note explaining why the schedule is paused.</param>
    public ScheduleBuilder StartPaused(bool paused = true, string? note = null)
    {
        _scheduleState = new ScheduleState
        {
            Paused = paused,
            Note = note
        };
        return this;
    }

    /// <summary>
    /// Creates a new schedule. Fails if a schedule with the same ID already exists.
    /// Use this when you want to ensure a brand new schedule is created.
    /// </summary>
    /// <returns>A XiansSchedule instance representing the created schedule.</returns>
    /// <exception cref="ScheduleAlreadyExistsException">Thrown if schedule already exists.</exception>
    public async Task<XiansSchedule> CreateAsync()
    {
        if (_scheduleSpec == null)
        {
            throw new InvalidScheduleSpecException(
                "Schedule specification is required. Use WithCronSchedule, WithIntervalSchedule, or WithScheduleSpec.");
        }

        return Workflow.InWorkflow 
            ? await CreateViaActivitiesAsync() 
            : await CreateViaTemporalClientAsync(checkExists: false);
    }

    /// <summary>
    /// Creates a schedule if it doesn't already exist. Returns existing schedule if found.
    /// This is idempotent - safe to call multiple times.
    /// </summary>
    /// <returns>A XiansSchedule instance representing the schedule (new or existing).</returns>
    public async Task<XiansSchedule> CreateIfNotExistsAsync()
    {
        if (_scheduleSpec == null)
        {
            throw new InvalidScheduleSpecException(
                "Schedule specification is required. Use WithCronSchedule, WithIntervalSchedule, or WithScheduleSpec.");
        }

        return Workflow.InWorkflow 
            ? await CreateViaActivitiesAsync() 
            : await CreateViaTemporalClientAsync(checkExists: true);
    }

    /// <summary>
    /// Creates schedule via activities (workflow context only).
    /// Activities maintain workflow determinism by isolating I/O operations.
    /// Search attributes are converted to serializable format and passed through activities.
    /// </summary>
    private async Task<XiansSchedule> CreateViaActivitiesAsync()
    {
        try
        {
            // Extract search attributes to serializable format
            var searchAttrs = ExtractSearchAttributesForSerialization();

            // Determine schedule type
            var isCronSchedule = _scheduleSpec?.CronExpressions?.Any() == true;
            var isIntervalSchedule = _scheduleSpec?.Intervals?.Any() == true;

            bool created;

            if (isCronSchedule && _scheduleSpec?.CronExpressions?.FirstOrDefault() != null)
            {
                var request = new CreateCronScheduleRequest
                {
                    ScheduleName = _scheduleName,
                    CronExpression = _scheduleSpec.CronExpressions.First(),
                    WorkflowInput = _workflowArgs ?? Array.Empty<object>(),
                    Timezone = _scheduleSpec.TimeZoneName,
                    IdPostfix = _idPostfix,
                    SearchAttributes = searchAttrs,
                    WorkflowType = _workflowType
                };

                created = await Workflow.ExecuteActivityAsync(
                    (Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivities act) => act.CreateScheduleIfNotExists(request),
                    Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivityOptions.GetStandardOptions());
            }
            else if (isIntervalSchedule && _scheduleSpec?.Intervals?.FirstOrDefault() != null)
            {
                var request = new CreateIntervalScheduleRequest
                {
                    ScheduleName = _scheduleName,
                    Interval = _scheduleSpec.Intervals.First().Every,
                    WorkflowInput = _workflowArgs ?? Array.Empty<object>(),
                    IdPostfix = _idPostfix,
                    SearchAttributes = searchAttrs,
                    WorkflowType = _workflowType
                };

                created = await Workflow.ExecuteActivityAsync(
                    (Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivities act) => act.CreateIntervalScheduleIfNotExists(request),
                    Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivityOptions.GetStandardOptions());
            }
            else
            {
                throw new InvalidScheduleSpecException(
                    "Complex schedule specifications not yet supported in workflow context. " +
                    "Use cron or interval schedules, or create the schedule outside the workflow.");
            }

            Workflow.Logger.LogDebug(
                created ? "Schedule '{ScheduleId}' created successfully" : "Schedule '{ScheduleId}' already exists",
                _scheduleName);

            // Return schedule handle with full tenant:agent:idPostfix:scheduleId pattern
            var fullScheduleId = BuildFullScheduleId();
            
            return new XiansSchedule(new ScheduleHandle(
                await _temporalService.GetClientAsync(),
                fullScheduleId));
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogError(ex, "Failed to create schedule '{ScheduleId}' via activities", _scheduleName);
            throw;
        }
    }

    /// <summary>
    /// Extracts search attributes to a serializable dictionary format.
    /// Prioritizes explicitly set attributes, falls back to workflow search attributes.
    /// Extracts common workflow search attributes: TenantId, Agent, UserId, idPostfix.
    /// </summary>
    private Dictionary<string, object>? ExtractSearchAttributesForSerialization()
    {
        var searchAttributes = _typedSearchAttributes ?? Workflow.TypedSearchAttributes;
        // Extract known search attributes to serializable format
        var result = new Dictionary<string, object>();
        
        // Extract common workflow search attributes
        ExtractSearchAttribute(searchAttributes, WorkflowConstants.Keys.TenantId, result);
        ExtractSearchAttribute(searchAttributes, WorkflowConstants.Keys.Agent, result);
        ExtractSearchAttribute(searchAttributes, WorkflowConstants.Keys.UserId, result);
        ExtractSearchAttribute(searchAttributes, WorkflowConstants.Keys.idPostfix, result);
        
        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Helper to extract a single search attribute if it exists.
    /// </summary>
    private void ExtractSearchAttribute(SearchAttributeCollection searchAttrs, string keyName, Dictionary<string, object> result)
    {
        try
        {
            var key = SearchAttributeKey.CreateKeyword(keyName);
            var value = searchAttrs.Get(key);
            if (value != null)
            {
                result[keyName] = value;
            }
        }
        catch
        {
            // Attribute doesn't exist or wrong type, skip it
        }
    }

    /// <summary>
    /// Gets the effective tenant ID based on agent scope and context.
    /// For system-scoped agents: uses tenant from current workflow context (must be in workflow/activity)
    /// For tenant-scoped agents: uses agent's registered tenant (must exist)
    /// All schedules MUST have a tenant ID for security and data isolation.
    /// </summary>
    private string GetEffectiveTenantId()
    {
        if (_agent.SystemScoped)
        {
            // System-scoped: use tenant from workflow context
            // XiansContext.TenantId will throw if not in workflow/activity context
            return XiansContext.TenantId;
        }
        else
        {
            // Tenant-scoped: must have a valid tenant ID
            return _agent.Options?.CertificateTenantId 
                ?? throw new InvalidOperationException(
                    "Tenant-scoped agent must have a valid tenant ID. XiansOptions not properly configured.");
        }
    }

    /// <summary>
    /// Builds the full schedule ID using the pattern: tenantId:agentName:idPostfix:scheduleId
    /// Uses instance variables and effective tenant ID to construct the full schedule identifier.
    /// </summary>
    /// <returns>The fully qualified schedule ID.</returns>
    private string BuildFullScheduleId()
    {
        var tenantId = GetEffectiveTenantId();
        var agentName = _agent.Name;
        return ScheduleIdHelper.BuildFullScheduleId(tenantId, agentName, _idPostfix, _scheduleName);
    }

    /// <summary>
    /// Gets memo for scheduled workflow executions.
    /// Merges system-required metadata (TenantId, AgentName, UserId, SystemScoped) with custom memo.
    /// Values are extracted from search attributes when available for consistency.
    /// </summary>
    private Dictionary<string, object> GetMemo(string tenantId)
    {
        // Get search attributes (explicit or from workflow context)
        var searchAttributes = _typedSearchAttributes 
            ?? (Workflow.InWorkflow ? Workflow.TypedSearchAttributes : null);

        // Extract values from search attributes when available, otherwise use defaults
        var agentName = GetSearchAttributeValue(searchAttributes, WorkflowConstants.Keys.Agent) 
            ?? _agent.Name;
        var userId = GetSearchAttributeValue(searchAttributes, WorkflowConstants.Keys.UserId) 
            ?? _agent.Options?.CertificateInfo?.UserId 
            ?? "system";


        // Start with system-required metadata
        var memo = new Dictionary<string, object>
        {
            { WorkflowConstants.Keys.TenantId, tenantId },
            { WorkflowConstants.Keys.Agent, agentName },
            { WorkflowConstants.Keys.UserId, userId },
            { WorkflowConstants.Keys.idPostfix, _idPostfix },
            { WorkflowConstants.Keys.SystemScoped, _agent.SystemScoped }
        };

        // Merge custom memo if provided
        if (_workflowMemo != null)
        {
            foreach (var kvp in _workflowMemo)
            {
                memo[kvp.Key] = kvp.Value;
            }
        }

        return memo;
    }

    /// <summary>
    /// Helper to safely get a search attribute value as string.
    /// </summary>
    private string? GetSearchAttributeValue(SearchAttributeCollection? searchAttrs, string keyName)
    {
        if (searchAttrs == null)
            return null;

        try
        {
            var key = SearchAttributeKey.CreateKeyword(keyName);
            var value = searchAttrs.Get(key);
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates schedule directly using Temporal client.
    /// Optionally checks for existence first to provide idempotent behavior.
    /// </summary>
    /// <param name="checkExists">If true, returns existing schedule instead of failing. If false, fails if schedule exists.</param>
    private async Task<XiansSchedule> CreateViaTemporalClientAsync(bool checkExists)
    {
        var logger = Workflow.InWorkflow ? Workflow.Logger : _logger;
        
        try
        {
            // Get Temporal client
            var client = await _temporalService.GetClientAsync();

            // Generate full schedule ID: tenantId:agent:idPostfix:scheduleId
            var fullScheduleId = BuildFullScheduleId();
            var tenantId = GetEffectiveTenantId();

            // Check if schedule already exists (only if checkExists is true)
            if (checkExists)
            {
                try
                {
                    var existingHandle = client.GetScheduleHandle(fullScheduleId);
                    await existingHandle.DescribeAsync();
                    
                    // Schedule exists, return it
                    logger.LogDebug("Schedule '{ScheduleId}' already exists, returning existing schedule", _scheduleName);
                    return new XiansSchedule(existingHandle);
                }
                catch (Temporalio.Exceptions.RpcException ex) when (
                    ex.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Schedule doesn't exist, continue with creation
                }
            }

            // Create the schedule
            var taskQueue = TenantContext.GetTaskQueueName(_workflowType, _agent.SystemScoped, tenantId);

            // Generate workflow ID prefix for scheduled executions - always includes tenant
            // Temporal will automatically append a unique suffix for each scheduled execution
            //var workflowId = $"{tenantId}:{_workflowType}:{_scheduleName}";
            var workflowId = ScheduleIdHelper.BuildFullWorkflowId(tenantId, _workflowType, _idPostfix);

            // Create schedule action using Temporal SDK pattern with search attributes and memo for workflow executions
            // Note: Workflow.TypedSearchAttributes only accessible in workflow context, not in activities
            var searchAttributes = _typedSearchAttributes 
                ?? (Workflow.InWorkflow ? Workflow.TypedSearchAttributes : null);
            
            var scheduleAction = ScheduleActionStartWorkflow.Create(
                _workflowType,
                _workflowArgs ?? Array.Empty<object>(),
                new(id: workflowId, taskQueue: taskQueue)
                {
                    RetryPolicy = _retryPolicy,
                    RunTimeout = _timeout,
                    TypedSearchAttributes = searchAttributes,
                    Memo = GetMemo(tenantId)
                });

            // Create the schedule with all properties in initializer (init-only properties)
            var schedule = new Schedule(
                Action: scheduleAction,
                Spec: _scheduleSpec ?? new ScheduleSpec())
            {
                Policy = _schedulePolicy ?? new SchedulePolicy(),
                State = _scheduleState ?? new ScheduleState()
            };

            logger.LogDebug(
                "Creating schedule '{ScheduleId}' for workflow '{WorkflowType}' on task queue '{TaskQueue}'",
                fullScheduleId, _workflowType, taskQueue);

            var handle = await client.CreateScheduleAsync(fullScheduleId, schedule);

            // Update schedule with search attributes (must be done after creation)
            await handle.UpdateAsync(scheduleUpdate =>
            {
                return new ScheduleUpdate(
                    scheduleUpdate.Description.Schedule,
                    TypedSearchAttributes: searchAttributes);
            });

            logger.LogDebug(
                "✅ Schedule '{ScheduleId}' created successfully. Agent='{AgentName}', SystemScoped={SystemScoped}, TenantId={TenantId}",
                fullScheduleId, _agent.Name, _agent.SystemScoped, tenantId);

            return new XiansSchedule(handle);
        }
        catch (Temporalio.Exceptions.ScheduleAlreadyRunningException ex)
        {
            logger.LogError(ex, "Schedule '{ScheduleId}' already exists", _scheduleName);
            throw new ScheduleAlreadyExistsException(_scheduleName, ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create schedule '{ScheduleId}'", _scheduleName);
            throw;
        }
    }

}


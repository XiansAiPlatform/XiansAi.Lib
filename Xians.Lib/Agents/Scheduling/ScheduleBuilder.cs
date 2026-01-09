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
    private readonly string _scheduleId;
    private readonly string _workflowType;
    private readonly XiansAgent _agent;
    private readonly ITemporalClientService _temporalService;
    private readonly ILogger<ScheduleBuilder> _logger;

    private ScheduleSpec? _scheduleSpec;
    private object[]? _workflowArgs;
    private Dictionary<string, object>? _workflowMemo;
    private Temporalio.Common.RetryPolicy? _retryPolicy;
    private TimeSpan? _timeout;
    private SchedulePolicy? _schedulePolicy;
    private ScheduleState? _scheduleState;

    internal ScheduleBuilder(
        string scheduleId,
        string workflowType,
        XiansAgent agent,
        ITemporalClientService temporalService)
    {
        _scheduleId = scheduleId ?? throw new ArgumentNullException(nameof(scheduleId));
        _workflowType = workflowType ?? throw new ArgumentNullException(nameof(workflowType));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _temporalService = temporalService ?? throw new ArgumentNullException(nameof(temporalService));
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<ScheduleBuilder>();
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
    /// Creates and starts the schedule.
    /// Automatically detects workflow context and uses activities when needed for determinism.
    /// </summary>
    /// <returns>A XiansSchedule instance representing the created schedule.</returns>
    public async Task<XiansSchedule> StartAsync()
    {
        if (_scheduleSpec == null)
        {
            throw new InvalidScheduleSpecException(
                "Schedule specification is required. Use WithCronSchedule, WithIntervalSchedule, or WithScheduleSpec.");
        }

        // Auto-detect workflow context and delegate to activity if needed
        if (Workflow.InWorkflow)
        {
            return await ExecuteViaSystemActivityAsync();
        }

        try
        {
            // Get Temporal client
            var client = await _temporalService.GetClientAsync();

            // Determine tenant context - ALL schedules must have a tenant ID
            // System-scoped: uses tenant from workflow context (must be in workflow/activity)
            // Tenant-scoped: uses agent's registered tenant
            var tenantId = GetEffectiveTenantId();
            var taskQueue = TenantContext.GetTaskQueueName(_workflowType, _agent.SystemScoped, tenantId, _agent.Name);

            // Generate workflow ID prefix for scheduled executions - always includes tenant
            // Temporal will automatically append a unique suffix for each scheduled execution
            var workflowId = $"{tenantId}:{_workflowType}:{_scheduleId}";

            // Create schedule action using Temporal SDK pattern with search attributes and memo for workflow executions
            var scheduleAction = ScheduleActionStartWorkflow.Create(
                _workflowType,
                _workflowArgs ?? Array.Empty<object>(),
                new(id: workflowId, taskQueue: taskQueue)
                {
                    RetryPolicy = _retryPolicy,
                    RunTimeout = _timeout,
                    TypedSearchAttributes = GetSearchAttributes(tenantId),
                    Memo = GetMemo(tenantId)
                });

            // Generate schedule ID with tenant prefix - always includes tenant
            var fullScheduleId = $"{tenantId}:{_scheduleId}";

            // Create the schedule with all properties in initializer (init-only properties)
            var schedule = new Schedule(
                Action: scheduleAction,
                Spec: _scheduleSpec ?? new ScheduleSpec())
            {
                Policy = _schedulePolicy ?? new SchedulePolicy(),
                State = _scheduleState ?? new ScheduleState()
            };

            _logger.LogInformation(
                "Creating schedule '{ScheduleId}' for workflow '{WorkflowType}' on task queue '{TaskQueue}'",
                fullScheduleId, _workflowType, taskQueue);

            var handle = await client.CreateScheduleAsync(fullScheduleId, schedule);

            // Update schedule with search attributes (must be done after creation)
            await handle.UpdateAsync(scheduleUpdate =>
            {
                return new ScheduleUpdate(
                    scheduleUpdate.Description.Schedule,
                    TypedSearchAttributes: GetSearchAttributes(tenantId));
            });

            _logger.LogInformation(
                "Schedule '{ScheduleId}' created successfully with search attributes. Agent='{AgentName}', SystemScoped={SystemScoped}, TenantId={TenantId}",
                fullScheduleId, _agent.Name, _agent.SystemScoped, tenantId ?? "(none)");

            return new XiansSchedule(handle);
        }
        catch (Temporalio.Exceptions.ScheduleAlreadyRunningException ex)
        {
            _logger.LogError(ex, "Schedule '{ScheduleId}' already exists", _scheduleId);
            throw new ScheduleAlreadyExistsException(_scheduleId, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule '{ScheduleId}'", _scheduleId);
            throw;
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
    /// Gets search attributes for both the schedule and scheduled workflow executions.
    /// Includes TenantId, AgentName, WorkflowType, and UserId for schedule and workflow tracking and filtering.
    /// </summary>
    private SearchAttributeCollection GetSearchAttributes(string tenantId)
    {
        var agentName = _agent.Name;
        var userId = _agent.Options?.CertificateInfo?.UserId ?? "system";

        var searchAttributesBuilder = new SearchAttributeCollection.Builder()
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.TenantId), tenantId)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.Agent), agentName)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.UserId), userId);

        return searchAttributesBuilder.ToSearchAttributeCollection();
    }

    /// <summary>
    /// Gets memo for scheduled workflow executions.
    /// Merges system-required metadata (TenantId, AgentName, UserId, SystemScoped) with custom memo.
    /// </summary>
    private Dictionary<string, object> GetMemo(string tenantId)
    {
        var agentName = _agent.Name;
        var userId = _agent.Options?.CertificateInfo?.UserId ?? "system";

        // Start with system-required metadata
        var memo = new Dictionary<string, object>
        {
            { WorkflowConstants.Keys.TenantId, tenantId },
            { WorkflowConstants.Keys.Agent, agentName },
            { WorkflowConstants.Keys.UserId, userId },
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
    /// Executes schedule creation via the system ScheduleActivities when called from a workflow.
    /// This maintains workflow determinism by delegating I/O operations to activities.
    /// </summary>
    private async Task<XiansSchedule> ExecuteViaSystemActivityAsync()
    {
        _logger.LogDebug("Detected workflow context - executing schedule creation via ScheduleActivities");

        try
        {
            // Determine if this is a cron or interval schedule
            var isCronSchedule = _scheduleSpec?.CronExpressions?.Any() == true;
            var isIntervalSchedule = _scheduleSpec?.Intervals?.Any() == true;

            bool created;

            if (Workflow.InWorkflow)
            {
                // WORKFLOW CONTEXT: Execute via activity for determinism
                if (isCronSchedule && _scheduleSpec?.CronExpressions?.FirstOrDefault() != null)
                {
                    // Cron-based schedule
                    var cronExpression = _scheduleSpec.CronExpressions.First();
                    var timezone = _scheduleSpec.TimeZoneName;

                    var request = new CreateCronScheduleRequest
                    {
                        ScheduleId = _scheduleId,
                        CronExpression = cronExpression,
                        WorkflowInput = _workflowArgs ?? Array.Empty<object>(),
                        Timezone = timezone
                    };

                    created = await Workflow.ExecuteActivityAsync(
                        (Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivities act) => act.CreateScheduleIfNotExists(request),
                        Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivityOptions.GetStandardOptions());
                }
                else if (isIntervalSchedule && _scheduleSpec?.Intervals?.FirstOrDefault() != null)
                {
                    // Interval-based schedule
                    var intervalSpec = _scheduleSpec.Intervals.First();
                    var interval = intervalSpec.Every;

                    var request = new CreateIntervalScheduleRequest
                    {
                        ScheduleId = _scheduleId,
                        Interval = interval,
                        WorkflowInput = _workflowArgs ?? Array.Empty<object>()
                    };

                    created = await Workflow.ExecuteActivityAsync(
                        (Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivities act) => act.CreateIntervalScheduleIfNotExists(request),
                        Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivityOptions.GetStandardOptions());
                }
                else
                {
                    throw new InvalidScheduleSpecException(
                        "Complex schedule specifications with calendars or custom specs are not yet supported in workflow context. " +
                        "Create the schedule outside the workflow using the direct SDK.");
                }
            }
            else
            {
                // ACTIVITY CONTEXT: Call Schedule SDK directly
                var workflow = XiansContext.CurrentWorkflow;
                
                if (await workflow.Schedules!.ExistsAsync(_scheduleId))
                {
                    created = false;
                }
                else if (isCronSchedule && _scheduleSpec?.CronExpressions?.FirstOrDefault() != null)
                {
                    // Cron-based schedule
                    var cronExpression = _scheduleSpec.CronExpressions.First();
                    var timezone = _scheduleSpec.TimeZoneName;

                    await workflow.Schedules!
                        .Create(_scheduleId)
                        .WithCronSchedule(cronExpression, timezone)
                        .WithInput(_workflowArgs ?? Array.Empty<object>())
                        .StartAsync();
                    
                    created = true;
                }
                else if (isIntervalSchedule && _scheduleSpec?.Intervals?.FirstOrDefault() != null)
                {
                    // Interval-based schedule
                    var intervalSpec = _scheduleSpec.Intervals.First();
                    var interval = intervalSpec.Every;

                    await workflow.Schedules!
                        .Create(_scheduleId)
                        .WithIntervalSchedule(interval)
                        .WithInput(_workflowArgs ?? Array.Empty<object>())
                        .StartAsync();
                    
                    created = true;
                }
                else
                {
                    throw new InvalidScheduleSpecException(
                        "Complex schedule specifications with calendars or custom specs are not yet supported. " +
                        "Use the direct Schedule SDK for advanced scenarios.");
                }
            }

            if (created)
            {
                if (Workflow.InWorkflow)
                    Workflow.Logger.LogInformation("✅ Schedule '{ScheduleId}' created successfully", _scheduleId);
                else
                    _logger.LogInformation("✅ Schedule '{ScheduleId}' created successfully", _scheduleId);
            }
            else
            {
                if (Workflow.InWorkflow)
                    Workflow.Logger.LogInformation("ℹ️ Schedule '{ScheduleId}' already exists", _scheduleId);
                else
                    _logger.LogInformation("ℹ️ Schedule '{ScheduleId}' already exists", _scheduleId);
            }

            // Verify schedule exists (context-aware)
            bool scheduleExists;
            if (Workflow.InWorkflow)
            {
                var request = new ScheduleExistsRequest { ScheduleId = _scheduleId };
                scheduleExists = await Workflow.ExecuteActivityAsync(
                    (Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivities act) => act.ScheduleExists(request),
                    Xians.Lib.Temporal.Workflows.Scheduling.ScheduleActivityOptions.GetQuickCheckOptions());
            }
            else
            {
                var workflow = XiansContext.CurrentWorkflow;
                scheduleExists = await workflow.Schedules!.ExistsAsync(_scheduleId);
            }

            if (!scheduleExists)
            {
                throw new InvalidOperationException($"Schedule '{_scheduleId}' was not created successfully");
            }

            // Create a placeholder schedule handle
            // Note: The actual handle operations will also need to be workflow-aware
            return new XiansSchedule(new ScheduleHandle(
                await _temporalService.GetClientAsync(),
                _scheduleId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create schedule '{ScheduleId}' via activity", _scheduleId);
            throw;
        }
    }
}


using Microsoft.Extensions.Logging;
using Temporalio.Client.Schedules;
using Temporalio.Common;
using Temporalio.Workflows;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common.MultiTenancy;

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
    /// Sets a cron-based schedule specification.
    /// </summary>
    /// <param name="cronExpression">Cron expression (e.g., "0 9 * * *" for daily at 9 AM).</param>
    /// <param name="timezone">Optional timezone (defaults to UTC).</param>
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
    /// Sets the input arguments for the workflow execution.
    /// </summary>
    /// <param name="args">Workflow input arguments.</param>
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
            var taskQueue = TenantContext.GetTaskQueueName(_workflowType, _agent.SystemScoped, tenantId);

            // Generate workflow ID prefix for scheduled executions - always includes tenant
            // Temporal will automatically append a unique suffix for each scheduled execution
            var workflowId = $"{tenantId}:{_agent.Name}:{_workflowType}:{_scheduleId}";

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
            .Set(SearchAttributeKey.CreateKeyword("tenantId"), tenantId)
            .Set(SearchAttributeKey.CreateKeyword("agent"), agentName)
            .Set(SearchAttributeKey.CreateKeyword("userId"), userId);

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
            { "tenantId", tenantId },
            { "agent", agentName },
            { "userId", userId },
            { "systemScoped", _agent.SystemScoped }
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

            if (isCronSchedule && _scheduleSpec?.CronExpressions?.FirstOrDefault() != null)
            {
                // Cron-based schedule
                var cronExpression = _scheduleSpec.CronExpressions.First();
                var timezone = _scheduleSpec.TimeZoneName;

                created = await Workflow.ExecuteActivityAsync(
                    (Xians.Lib.Workflows.Scheduling.ScheduleActivities act) => act.CreateScheduleIfNotExists(
                        _scheduleId,
                        cronExpression,
                        _workflowArgs ?? Array.Empty<object>(),
                        timezone),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(2),
                        RetryPolicy = new Temporalio.Common.RetryPolicy
                        {
                            MaximumAttempts = 3,
                            InitialInterval = TimeSpan.FromSeconds(5),
                            BackoffCoefficient = 2.0f
                        }
                    });
            }
            else if (isIntervalSchedule && _scheduleSpec?.Intervals?.FirstOrDefault() != null)
            {
                // Interval-based schedule
                var intervalSpec = _scheduleSpec.Intervals.First();
                var interval = intervalSpec.Every;

                created = await Workflow.ExecuteActivityAsync(
                    (Xians.Lib.Workflows.Scheduling.ScheduleActivities act) => act.CreateIntervalScheduleIfNotExists(
                        _scheduleId,
                        interval,
                        _workflowArgs ?? Array.Empty<object>()),
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(2),
                        RetryPolicy = new Temporalio.Common.RetryPolicy
                        {
                            MaximumAttempts = 3,
                            InitialInterval = TimeSpan.FromSeconds(5),
                            BackoffCoefficient = 2.0f
                        }
                    });
            }
            else
            {
                throw new InvalidScheduleSpecException(
                    "Complex schedule specifications with calendars or custom specs are not yet supported in workflow context. " +
                    "Create the schedule outside the workflow using the direct SDK.");
            }

            if (created)
            {
                Workflow.Logger.LogInformation("✅ Schedule '{ScheduleId}' created successfully", _scheduleId);
            }
            else
            {
                Workflow.Logger.LogInformation("ℹ️ Schedule '{ScheduleId}' already exists", _scheduleId);
            }

            // Return a schedule handle (note: this gets the handle which involves I/O, but it's safe via activity)
            var scheduleExists = await Workflow.ExecuteActivityAsync(
                (Xians.Lib.Workflows.Scheduling.ScheduleActivities act) => act.ScheduleExists(_scheduleId),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(30) });

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


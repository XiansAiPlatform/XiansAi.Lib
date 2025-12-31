using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Xians.Lib.Workflows.Scheduling;
using Xians.Lib.Workflows.Messaging;
using Xians.Lib.Workflows.Knowledge;
using Xians.Lib.Workflows.Documents;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Handles registration of activities for Temporal workers.
/// Centralizes activity registration logic and provides a cleaner API.
/// </summary>
internal class ActivityRegistrar
{
    private readonly XiansAgent _agent;
    private readonly ILogger _logger;

    public ActivityRegistrar(XiansAgent agent, ILogger logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers all system activities (Schedule, Message, Knowledge, Document).
    /// </summary>
    public int RegisterSystemActivities(TemporalWorkerOptions workerOptions, string workflowType)
    {
        int registeredCount = 0;

        // Schedule activities (always available, no dependencies)
        registeredCount += TryRegisterActivity(
            workerOptions,
            workflowType,
            "ScheduleActivities",
            () => new ScheduleActivities(),
            typeof(ScheduleActivities));

        // HTTP-dependent activities
        if (_agent.HttpService != null)
        {
            registeredCount += TryRegisterActivity(
                workerOptions,
                workflowType,
                "MessageActivities",
                () => new MessageActivities(_agent.HttpService.Client),
                typeof(MessageActivities));

            registeredCount += TryRegisterActivity(
                workerOptions,
                workflowType,
                "KnowledgeActivities",
                () => new KnowledgeActivities(_agent.HttpService.Client, _agent.CacheService),
                typeof(KnowledgeActivities));

            registeredCount += TryRegisterActivity(
                workerOptions,
                workflowType,
                "DocumentActivities",
                () => new DocumentActivities(_agent.HttpService.Client),
                typeof(DocumentActivities));
        }
        else
        {
            _logger.LogWarning(
                "HTTP service not available for workflow '{WorkflowType}' - Message, Knowledge, and Document activities will not be registered",
                workflowType);
        }

        _logger.LogInformation(
            "Registered {Count} system activities for workflow '{WorkflowType}'",
            registeredCount, workflowType);

        return registeredCount;
    }

    /// <summary>
    /// Registers user-provided activity instances.
    /// </summary>
    public int RegisterUserActivityInstances(
        TemporalWorkerOptions workerOptions,
        string workflowType,
        IEnumerable<object> activityInstances)
    {
        int registeredCount = 0;

        foreach (var activityInstance in activityInstances)
        {
            var activityType = activityInstance.GetType();
            if (TryRegisterActivity(
                workerOptions,
                workflowType,
                activityType.Name,
                () => activityInstance,
                activityType) > 0)
            {
                registeredCount++;
            }
        }

        return registeredCount;
    }

    /// <summary>
    /// Registers user-provided activity types (creates instances).
    /// </summary>
    public int RegisterUserActivityTypes(
        TemporalWorkerOptions workerOptions,
        string workflowType,
        IEnumerable<Type> activityTypes)
    {
        int registeredCount = 0;

        foreach (var activityType in activityTypes)
        {
            if (TryRegisterActivity(
                workerOptions,
                workflowType,
                activityType.Name,
                () => Activator.CreateInstance(activityType),
                activityType) > 0)
            {
                registeredCount++;
            }
        }

        return registeredCount;
    }

    /// <summary>
    /// Attempts to register a single activity with error handling.
    /// Returns 1 if successful, 0 if failed.
    /// </summary>
    private int TryRegisterActivity(
        TemporalWorkerOptions workerOptions,
        string workflowType,
        string activityName,
        Func<object?> activityFactory,
        Type activityType)
    {
        try
        {
            var instance = activityFactory();
            if (instance == null)
            {
                _logger.LogWarning(
                    "Failed to create instance of activity '{ActivityName}' for workflow '{WorkflowType}' - factory returned null",
                    activityName, workflowType);
                return 0;
            }

            workerOptions.AddAllActivities(activityType, instance);
            _logger.LogDebug(
                "Registered activity '{ActivityName}' for workflow '{WorkflowType}'",
                activityName, workflowType);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not register activity '{ActivityName}' for workflow '{WorkflowType}'",
                activityName, workflowType);
            return 0;
        }
    }
}


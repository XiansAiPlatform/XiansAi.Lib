using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Temporalio.Common;

namespace Xians.Lib.Temporal;

/// <summary>
/// Base class for executing operations that work in both workflow and non-workflow contexts.
/// Automatically handles activity execution in workflows and direct service calls in activities.
/// Eliminates code duplication for context-aware operations.
/// </summary>
/// <typeparam name="TActivity">The Temporal activity class type.</typeparam>
/// <typeparam name="TService">The service class type for direct calls.</typeparam>
public abstract class ContextAwareActivityExecutor<TActivity, TService>
    where TActivity : class
    where TService : class
{
    private readonly ILogger _logger;

    protected ContextAwareActivityExecutor(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates an instance of the service for direct (non-workflow) calls.
    /// Override this in derived classes to provide the appropriate service.
    /// </summary>
    protected abstract TService CreateService();

    /// <summary>
    /// Gets the default activity options used when executing activities from workflows.
    /// Override to customize timeouts and retry policies for specific domains.
    /// </summary>
    protected virtual ActivityOptions GetDefaultActivityOptions()
    {
        return new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromSeconds(30),
            RetryPolicy = new RetryPolicy
            {
                MaximumAttempts = 3,
                InitialInterval = TimeSpan.FromSeconds(1),
                MaximumInterval = TimeSpan.FromSeconds(10),
                BackoffCoefficient = 2
            }
        };
    }

    /// <summary>
    /// Maps a failure raised inside an activity back to the exception type this SDK's contract
    /// documents, so callers can catch the same type whether the call ran in a workflow or not.
    /// Returns null to let the original <see cref="ActivityFailureException"/> propagate.
    /// </summary>
    /// <remarks>
    /// Only return exceptions deriving from <see cref="FailureException"/>. The worker registers a
    /// narrow <c>WorkflowFailureExceptionTypes</c> list, so any other exception thrown from workflow
    /// code fails the workflow <em>task</em> and retries forever rather than failing the run - turning
    /// a clean error into a stuck workflow. That is why plain BCL types such as
    /// <see cref="InvalidOperationException"/> are deliberately not translated here.
    /// </remarks>
    /// <param name="failure">The application failure carried by the activity failure.</param>
    protected virtual Exception? TranslateActivityFailure(ApplicationFailureException failure) => null;

    private Exception? Translate(ActivityFailureException ex)
    {
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is ApplicationFailureException failure)
            {
                return TranslateActivityFailure(failure);
            }
        }

        return null;
    }

    /// <summary>
    /// Executes an operation that returns a result.
    /// In workflow context: executes as a Temporal activity.
    /// In activity/non-workflow context: calls the service directly.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="activityCall">Lambda expression for the activity call.</param>
    /// <param name="serviceCall">Delegate for the direct service call.</param>
    /// <param name="options">Optional activity options (uses defaults if not provided).</param>
    /// <param name="operationName">Optional operation name for logging.</param>
    /// <returns>The operation result.</returns>
    protected async Task<TResult> ExecuteAsync<TResult>(
        Expression<Func<TActivity, Task<TResult>>> activityCall,
        Func<TService, Task<TResult>> serviceCall,
        ActivityOptions? options = null,
        string? operationName = null)
    {
        var opName = operationName ?? ExtractOperationName(activityCall);

        if (Workflow.InWorkflow)
        {
            _logger.LogDebug(
                "Executing {Operation} via activity in workflow context",
                opName);

            try
            {
                return await Workflow.ExecuteActivityAsync(
                    activityCall,
                    options ?? GetDefaultActivityOptions());
            }
            catch (ActivityFailureException ex) when (Translate(ex) is { } translated)
            {
                throw translated;
            }
        }
        else
        {
            _logger.LogDebug(
                "Executing {Operation} via direct service call in activity context",
                opName);

            var service = CreateService();
            return await serviceCall(service);
        }
    }

    /// <summary>
    /// Executes an operation that doesn't return a result.
    /// In workflow context: executes as a Temporal activity.
    /// In activity/non-workflow context: calls the service directly.
    /// </summary>
    /// <param name="activityCall">Lambda expression for the activity call.</param>
    /// <param name="serviceCall">Delegate for the direct service call.</param>
    /// <param name="options">Optional activity options (uses defaults if not provided).</param>
    /// <param name="operationName">Optional operation name for logging.</param>
    protected async Task ExecuteAsync(
        Expression<Func<TActivity, Task>> activityCall,
        Func<TService, Task> serviceCall,
        ActivityOptions? options = null,
        string? operationName = null)
    {
        var opName = operationName ?? ExtractOperationName(activityCall);

        if (Workflow.InWorkflow)
        {
            _logger.LogDebug(
                "Executing {Operation} via activity in workflow context",
                opName);

            try
            {
                await Workflow.ExecuteActivityAsync(
                    activityCall,
                    options ?? GetDefaultActivityOptions());
            }
            catch (ActivityFailureException ex) when (Translate(ex) is { } translated)
            {
                throw translated;
            }
        }
        else
        {
            _logger.LogDebug(
                "Executing {Operation} via direct service call in activity context",
                opName);

            var service = CreateService();
            await serviceCall(service);
        }
    }

    /// <summary>
    /// Extracts the method name from the activity call expression for logging.
    /// </summary>
    private string ExtractOperationName<TResult>(Expression<Func<TActivity, Task<TResult>>> activityCall)
    {
        if (activityCall.Body is MethodCallExpression methodCall)
        {
            return methodCall.Method.Name;
        }
        return "UnknownOperation";
    }

    /// <summary>
    /// Extracts the method name from the activity call expression for logging (void version).
    /// </summary>
    private string ExtractOperationName(Expression<Func<TActivity, Task>> activityCall)
    {
        if (activityCall.Body is MethodCallExpression methodCall)
        {
            return methodCall.Method.Name;
        }
        return "UnknownOperation";
    }
}







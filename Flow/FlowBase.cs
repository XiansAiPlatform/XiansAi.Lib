using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace XiansAi.Flow;

public abstract class FlowBase
{
    private readonly ILogger _logger;
    public FlowBase()
    {
        _logger = Globals.LogFactory.CreateLogger<FlowBase>();
    }

    public Task<TResult> RunActivityAsync<TActivityInstance, TResult>(Expression<Func<TActivityInstance, Task<TResult>>> activityCall)
    {
        var methodName = ((MethodCallExpression)activityCall.Body).Method.Name;
        
        // Extract parameter values by compiling and executing the expressions
        var parameterInfo = string.Join(", ", ((MethodCallExpression)activityCall.Body).Arguments
            .Select(arg => {
                try {
                    // Compile and evaluate the expression to get the actual value
                    var valueExpression = Expression.Lambda(arg).Compile();
                    var value = valueExpression.DynamicInvoke();
                    return value?.ToString() ?? "null";
                }
                catch (Exception)
                {
                    // Fallback to expression string if evaluation fails
                    return arg.ToString();
                }
            }));
        
        _logger.LogInformation("Invoking agent {MethodName} with parameters [{Parameters}] ({WorkflowId})", 
            methodName, parameterInfo, Workflow.Info.WorkflowId);
        
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) };
        return Workflow.ExecuteActivityAsync(activityCall, options);    
    }

}
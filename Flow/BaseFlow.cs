using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace XiansAi.Flow;

public abstract class BaseFlow
{
    public  Task<TResult> RunActivity<TActivityInstance, TResult>(Expression<Func<TActivityInstance, Task<TResult>>> activityCall)
    {
        var logger = Globals.LogFactory.CreateLogger<BaseFlow>();
        logger.LogInformation("Invoking agent " + ((MethodCallExpression)activityCall.Body).Method.Name + "(" + Workflow.Info.WorkflowId + ")");
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) };
        return Workflow.ExecuteActivityAsync(activityCall, options);    
    }
}
using System.Reflection;
using XiansAi.Activity;
using XiansAi.Models;
using Microsoft.Extensions.Logging;

namespace XiansAi.Flow;

/// <summary>
/// Interface for extracting workflow metadata from flow definitions.
/// </summary>
/// <typeparam name="TClass">The workflow class type</typeparam>
public interface IFlowMetadataService<TClass>
{
    /// <summary>
    /// Extracts workflow information from a flow definition.
    /// </summary>
    /// <param name="flow">The flow information to extract from</param>
    /// <returns>A FlowDefinition containing the extracted metadata</returns>
    FlowDefinition ExtractFlowInformation(FlowInfo<TClass> flow);
}

/// <summary>
/// Service for extracting and processing workflow metadata.
/// </summary>
/// <typeparam name="TClass">The workflow class type</typeparam>
public class FlowMetadataService<TClass> : IFlowMetadataService<TClass>
{
    private readonly ILogger<FlowMetadataService<TClass>> _logger;

    /// <summary>
    /// Initializes a new instance of the FlowMetadataService class.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LogFactory is not initialized</exception>
    public FlowMetadataService()
    {
        _logger = Globals.LogFactory?.CreateLogger<FlowMetadataService<TClass>>() 
            ?? throw new InvalidOperationException("LogFactory not initialized");
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when flow is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when workflow attributes are missing or invalid</exception>
    public FlowDefinition ExtractFlowInformation(FlowInfo<TClass> flow)
    {
        ArgumentNullException.ThrowIfNull(flow, nameof(flow));

        var workflowType = typeof(TClass);
        var flowAttribute = workflowType.GetCustomAttribute<Temporalio.Workflows.WorkflowAttribute>();
        
        if (flowAttribute == null)
        {
            throw new InvalidOperationException(
                $"Class {workflowType.Name} must have WorkflowAttribute");
        }

        var workflowRunMethod = workflowType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<Temporalio.Workflows.WorkflowRunAttribute>() != null);

        if (workflowRunMethod == null)
        {
            throw new InvalidOperationException(
                $"Class {workflowType.Name} must have a method with WorkflowRunAttribute");
        }

        try
        {
            var className = workflowType.FullName ?? workflowType.Name;
            var flowName = flowAttribute.Name ?? workflowType.Name;
            var parameters = ExtractParameters(workflowRunMethod);
            var activities = ExtractActivityInformation(flow);

            _logger.LogDebug("Successfully extracted flow information for {FlowName}", flowName);

            return new FlowDefinition
            {
                Parameters = parameters,
                TypeName = flowName,
                ClassName = className,
                Activities = activities
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract flow information for {WorkflowType}", workflowType.Name);
            throw;
        }
    }

    /// <summary>
    /// Extracts parameter definitions from a method.
    /// </summary>
    private static List<ParameterDefinition> ExtractParameters(MethodInfo method)
    {
        return method.GetParameters()
            .Select(p => new ParameterDefinition 
            {
                Name = p.Name ?? "unnamed",
                Type = p.ParameterType.Name
            })
            .ToList();
    }

    /// <summary>
    /// Extracts activity information from a flow definition.
    /// </summary>
    private ActivityDefinition[] ExtractActivityInformation(FlowInfo<TClass> flow)
    {
        var activities = new List<ActivityDefinition>();

        foreach (var activity in flow.GetProxyActivities())
        {
            var type = activity.Key;
            var agentAttribute = type.GetCustomAttribute<AgentsAttribute>();
            var instructionsAttribute = type.GetCustomAttribute<InstructionsAttribute>();
            
            if (agentAttribute == null)
            {
                _logger.LogDebug("Skipping activity {ActivityType} without AgentAttribute", type.Name);
                continue;
            }

            try
            {
                var activityMethods = type.GetMethods()
                    .Where(m => m.GetCustomAttribute<Temporalio.Activities.ActivityAttribute>() != null);

                foreach (var method in activityMethods)
                {
                    var activityAttribute = method.GetCustomAttribute<Temporalio.Activities.ActivityAttribute>();
                    activities.Add(new ActivityDefinition
                    {
                        Agents = agentAttribute.Names.ToList(),
                        Instructions = instructionsAttribute?.Instructions.ToList() ?? [],
                        ActivityName = activityAttribute?.Name ?? method.Name,
                        Parameters = ExtractParameters(method)
                    });

                    _logger.LogDebug("Added activity definition for {ActivityName}", method.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract activity information for {ActivityType}", type.Name);
                throw;
            }
        }

        return activities.ToArray();
    }
}

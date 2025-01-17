using System.Reflection;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using XiansAi.Activity;
using XiansAi.Models;

namespace XiansAi.Flow;

/// <summary>
/// Manages workflow activity registration and metadata for a specific workflow class.
/// </summary>
/// <typeparam name="TClass">The workflow class type</typeparam>
public class FlowInfo<TClass>
{
    private readonly Dictionary<Type, object> _stubProxies = new();
    private readonly List<BaseAgentStub> _stubs = new();
    private readonly List<(Type @interface, object stub, object proxy)> _objects = new();
    private readonly ILogger<FlowInfo<TClass>> _logger = Globals.LogFactory.CreateLogger<FlowInfo<TClass>>();

    /// <summary>
    /// Registers an activity implementation with its interface.
    /// </summary>
    /// <typeparam name="IActivity">The activity interface type</typeparam>
    /// <param name="activity">The activity implementation instance</param>
    /// <returns>The current FlowInfo instance for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when activity is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when IActivity is not an interface</exception>
    public FlowInfo<TClass> AddActivities<IActivity>(BaseAgentStub stub) 
        where IActivity : class
    {
        Console.WriteLine($"Adding activities for {stub.GetType().Name}");
        _logger.LogDebug($"Adding activities for {stub.GetType().Name}");
        ArgumentNullException.ThrowIfNull(stub, nameof(stub));

        var interfaceType = typeof(IActivity);
        if (!interfaceType.IsInterface)
        {
            throw new InvalidOperationException($"Type parameter {interfaceType.Name} must be an interface");
        }

        try
        {
            _stubs.Add(stub);
            
            var activityType = stub.GetType();
            var proxyCreateMethod = typeof(ActivityTrackerProxy<,>)
                .MakeGenericType(interfaceType, activityType)
                .GetMethod("Create") 
                ?? throw new InvalidOperationException("Failed to find Create method on ActivityTrackerProxy");

            var stubProxy = proxyCreateMethod.Invoke(null, new[] { stub })
                ?? throw new InvalidOperationException("Failed to create activity proxy");

            Console.WriteLine($"Activity proxy created: {stubProxy} for interface {interfaceType.Name}");

            _stubProxies[interfaceType] = stubProxy;

            _objects.Add((interfaceType, stub, stubProxy));
            return this;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to add activity for interface {interfaceType.Name}", ex);
        }
    }

    /// <summary>
    /// Gets the registered activity implementations.
    /// </summary>
    /// <returns>Dictionary of interface types to activity implementations</returns>
    public List<BaseAgentStub> GetStubs()
    {
        return _stubs;
    }

    /// <summary>
    /// Gets the registered activity proxies.
    /// </summary>
    /// <returns>Dictionary of interface types to activity proxies</returns>
    public IReadOnlyDictionary<Type, object> GetStubProxies()
    {
        return _stubProxies;
    }

    public List<(Type @interface, object stub, object proxy)> GetObjects()
    {
        return _objects;
    }


    /// <summary>
    /// Gets the workflow name from the WorkflowAttribute or class name.
    /// </summary>
    /// <returns>The workflow name</returns>
    /// <exception cref="InvalidOperationException">Thrown when WorkflowAttribute is missing</exception>
    public string GetWorkflowName() 
    {
        var workflowClass = typeof(TClass);
        var workflowAttr = workflowClass.GetCustomAttribute<WorkflowAttribute>();
        
        if (workflowAttr == null)
        {
            throw new InvalidOperationException(
                $"Workflow {workflowClass.Name} is missing required WorkflowAttribute");
        }
        _logger.LogDebug($"Workflow name: {workflowAttr.Name ?? workflowClass.Name}");

        return workflowAttr.Name ?? workflowClass.Name;
    }

    /// <summary>
    /// Gets the parameters of the workflow's run method.
    /// </summary>
    /// <returns>List of parameter information for the workflow run method</returns>
    public List<ParameterDefinition> GetParameters()
    {
        var workflowType = typeof(TClass);
        var workflowRunMethod = workflowType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<WorkflowRunAttribute>() != null);

        return workflowRunMethod?.GetParameters().Select(p => new ParameterDefinition {
                Name = p.Name,
                Type = p.ParameterType.Name
            }).ToList() ?? [];
    }
}
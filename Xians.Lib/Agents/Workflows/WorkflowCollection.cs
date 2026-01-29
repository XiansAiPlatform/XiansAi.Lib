using System.Reflection;
using Xians.Lib.Agents.Workflows.Models;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Temporal.Workflows;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Xians.Lib.Agents.Workflows;

/// <summary>
/// Manages the collection of workflows for an agent.
/// </summary>
public class WorkflowCollection
{
    private readonly XiansAgent _agent;
    private readonly WorkflowDefinitionUploader? _uploader;
    private readonly ILogger<WorkflowCollection> _logger;
    private readonly List<XiansWorkflow> _workflows = new();

    internal WorkflowCollection(XiansAgent agent, WorkflowDefinitionUploader? uploader)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _uploader = uploader;
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<WorkflowCollection>();
    }

    /// <summary>
    /// Defines a built-in workflow for the agent using the platform-provided workflow implementation.
    /// Creates a dynamic class that extends BuiltinWorkflow with a [Workflow] attribute in the format: {AgentName}:{WorkflowName}
    /// Built-in workflows are always activable.
    /// </summary>
    /// <param name="name">The name for the workflow (e.g., "Conversational", "Web").</param>
    /// <param name="options">Workflow configuration options. If null, uses default options.</param>
    /// <returns>A new built-in XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow with the same name already exists.</exception>
    public XiansWorkflow DefineBuiltIn(string name, WorkflowOptions? options = null)
    {
        options ??= new WorkflowOptions();
        
        // Built-in workflows are always activable
        options.Activable = true;
        
        // Check if workflow with same name already exists
        if (_workflows.Any(w => w.Name == name))
        {
            throw new InvalidOperationException($"A workflow with the name '{name}' has already been registered.");
        }
        
        // Create the workflow type name in the format: {AgentName}:{WorkflowName}
        var workflowType = $"{_agent.Name}:{name}";
        
        // Dynamically create a class that extends BuiltinWorkflow with the [Workflow] attribute
        var dynamicWorkflowClassType = DynamicWorkflowTypeBuilder.GetOrCreateType(workflowType);
        
        // Create the XiansWorkflow instance with the dynamic type
        var workflow = new XiansWorkflow(
            _agent, 
            workflowType, 
            name, 
            options, 
            isBuiltIn: true, 
            workflowClassType: dynamicWorkflowClassType);
        
        _workflows.Add(workflow);
        
        return workflow;
    }

    /// <summary>
    /// Defines a built-in workflow for the agent using the platform-provided workflow implementation.
    /// Creates a dynamic class that extends BuiltinWorkflow with a [Workflow] attribute in the format: {AgentName}:{WorkflowName}
    /// </summary>
    /// <param name="name">The name for the workflow (e.g., "Conversational", "Web").</param>
    /// <param name="maxConcurrent">Maximum concurrent workflow task executions.</param>
    /// <returns>A new built-in XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow with the same name already exists.</exception>
    [Obsolete("Use DefineBuiltIn(string name, WorkflowOptions? options = null) instead.")]
    public XiansWorkflow DefineBuiltIn(string name, int maxConcurrent)
    {
        return DefineBuiltIn(name, new WorkflowOptions { MaxConcurrent = maxConcurrent });
    }

    /// <summary>
    /// Defines a built-in Supervisor Workflow for the agent.
    /// This is a shorthand for DefineBuiltIn(WorkflowConstants.WorkflowTypes.Supervisor).
    /// </summary>
    /// <param name="options">Workflow configuration options. If null, uses default options.</param>
    /// <returns>A new built-in XiansWorkflow instance for Supervisor Workflow.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow with the same name already exists.</exception>
    public XiansWorkflow DefineSupervisor(WorkflowOptions? options = null)
    {
        return DefineBuiltIn(WorkflowConstants.WorkflowTypes.Supervisor, options);
    }

    /// <summary>
    /// Defines a built-in Integrator Workflow for the agent.
    /// This is a shorthand for DefineBuiltIn(WorkflowConstants.WorkflowTypes.Integrator).
    /// </summary>
    /// <param name="options">Workflow configuration options. If null, uses default options.</param>
    /// <returns>A new built-in XiansWorkflow instance for Integrator Workflow.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow with the same name already exists.</exception>
    public XiansWorkflow DefineIntegrator(WorkflowOptions? options = null)
    {
        return DefineBuiltIn(WorkflowConstants.WorkflowTypes.Integrator, options);
    }

    /// <summary>
    /// Defines a custom workflow for the agent.
    /// </summary>
    /// <typeparam name="T">The custom workflow type.</typeparam>
    /// <param name="options">Workflow configuration options. If null, uses default options.</param>
    /// <returns>A new custom XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow of the same type already exists.</exception>
    public XiansWorkflow DefineCustom<T>(WorkflowOptions? options = null) where T : class
    {
        return DefineCustomInternal<T>(options, validateAgentPrefix: true);
    }

    /// <summary>
    /// Defines a custom workflow for the agent.
    /// </summary>
    /// <typeparam name="T">The custom workflow type.</typeparam>
    /// <param name="maxConcurrent">Maximum concurrent workflow task executions.</param>
    /// <returns>A new custom XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow of the same type already exists.</exception>
    [Obsolete("Use DefineCustom<T>(WorkflowOptions? options = null) instead.")]
    public XiansWorkflow DefineCustom<T>(int maxConcurrent) where T : class
    {
        return DefineCustom<T>(new WorkflowOptions { MaxConcurrent = maxConcurrent });
    }

    /// <summary>
    /// Internal method to define a custom workflow with optional agent prefix validation.
    /// </summary>
    private XiansWorkflow DefineCustomInternal<T>(WorkflowOptions? options, bool validateAgentPrefix) where T : class
    {
        options ??= new WorkflowOptions();
        
        // Get workflow type from [Workflow] attribute if present, otherwise use class name
        var workflowType = GetWorkflowTypeFromAttribute<T>(validateAgentPrefix) ?? typeof(T).Name;

        
        // Check if workflow with same type already exists
        if (_workflows.Any(w => w.WorkflowType == workflowType))
        {
            throw new InvalidOperationException($"A workflow of type '{workflowType}' has already been registered.");
        }
        
        var workflow = new XiansWorkflow(_agent, workflowType, null, options, isBuiltIn: false, workflowClassType: typeof(T));
        _workflows.Add(workflow);
        
        // Note: Workflow definition will be uploaded when RunAllAsync() is called
        
        return workflow;
    }

    /// <summary>
    /// Enables human-in-the-loop (HITL) task support for this agent.
    /// Creates a worker that can handle task assignments requiring human interaction.
    /// </summary>
    /// <param name="options">Workflow configuration options. If null, uses default options.</param>
    /// <returns>The WorkflowCollection instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when task workflow has already been enabled.</exception>
    public async Task<WorkflowCollection> WithTasks(WorkflowOptions? options = null)
    {
        options ??= new WorkflowOptions();
        
        // Task workflows are not activable (cannot be triggered directly)
        options.Activable = false;
        
        // Create the workflow type name using centralized helper
        var workflowType = WorkflowConstants.WorkflowTypes.GetTaskWorkflowType(_agent.Name);
        
        // Check if task workflow already exists
        if (_workflows.Any(w => w.WorkflowType == workflowType))
        {
            throw new InvalidOperationException("Task workflow has already been enabled for this agent.");
        }
        
        // Dynamically create a class that extends TaskWorkflow with the [Workflow] attribute
        var dynamicTaskWorkflowType = DynamicWorkflowTypeBuilder.GetOrCreateTaskWorkflowType(workflowType);
        
        var workflow = new XiansWorkflow(
            _agent, 
            workflowType, 
            null, 
            options, 
            isBuiltIn: false, 
            workflowClassType: dynamicTaskWorkflowType);
        
        _workflows.Add(workflow);
        
        // Upload the workflow definition to the server immediately
        await UploadWorkflowDefinitionAsync(workflow);
        
        return this;
    }

    /// <summary>
    /// Enables human-in-the-loop (HITL) task support for this agent.
    /// Creates a worker that can handle task assignments requiring human interaction.
    /// </summary>
    /// <param name="maxConcurrent">Maximum concurrent task workflow executions.</param>
    /// <returns>The WorkflowCollection instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when task workflow has already been enabled.</exception>
    [Obsolete("Use WithTasks(WorkflowOptions? options = null) instead.")]
    public async Task<WorkflowCollection> WithTasks(int maxConcurrent)
    {
        return await WithTasks(new WorkflowOptions { MaxConcurrent = maxConcurrent });
    }


    /// <summary>
    /// Uploads a workflow definition to the server.
    /// </summary>
    private async Task UploadWorkflowDefinitionAsync(XiansWorkflow workflow)
    {
        if (_uploader == null)
        {
            return; // No uploader configured, skip
        }

        try
        {
            var workflowClassType = workflow.GetWorkflowClassType();
            var source = workflowClassType != null ? ReadSource(workflowClassType) : null;
            
            // Only set Source if we actually found source code
            var sourceToUpload = !string.IsNullOrWhiteSpace(source) ? source : null;
            
            var parameters = ExtractWorkflowParameters(workflow);
            var activities = ExtractActivityDefinitions(workflow);

            // For custom workflows, extract name from WorkflowType (2nd part after splitting by ':')
            var workflowName = workflow.Name;
            if (workflowName == null && workflow.WorkflowType.Contains(':'))
            {
                var parts = workflow.WorkflowType.Split(':', 2);
                if (parts.Length == 2)
                {
                    workflowName = parts[1];
                }
            }
            
            var definition = new WorkflowDefinition
            {
                Agent = _agent.Name,
                WorkflowType = workflow.WorkflowType,
                Name = workflowName,
                Summary = ExtractWorkflowSummary(workflow),
                SystemScoped = _agent.SystemScoped,
                Workers = workflow.Workers,
                Activable = workflow.Activable,
                ActivityDefinitions = [],
                ParameterDefinitions = ExtractWorkflowParameters(workflow),
                Source = sourceToUpload
            };
            
            _logger.LogInformation(
                "📤 Uploading workflow definition: WorkflowType={WorkflowType}, Name={WorkflowName}, Parameters={ParameterCount}, Activities={ActivityCount}, HasSource={HasSource}, Activities=[{ActivityNames}]",
                workflow.WorkflowType,
                workflowName ?? "(null - THIS WILL CAUSE SG_InputParameters TO BE DISCONNECTED!)",
                parameters.Count,
                activities.Length,
                !string.IsNullOrWhiteSpace(sourceToUpload),
                string.Join(", ", activities.Select(a => a.ActivityName)));
            
            // Warn if Name is missing for BuiltinWorkflow subclasses - this causes visualization issues
            if (workflowName == null && workflowClassType != null && typeof(BuiltinWorkflow).IsAssignableFrom(workflowClassType))
            {
                _logger.LogWarning(
                    "⚠️ WARNING: Name field is NULL for BuiltinWorkflow subclass '{WorkflowType}'. " +
                    "This will cause SG_InputParameters to appear disconnected in the visualization. " +
                    "Expected format: 'AgentName:WorkflowName'",
                    workflow.WorkflowType);
            }

            await _uploader.UploadWorkflowDefinitionAsync(definition);
            
            _logger.LogInformation(
                "✅ Successfully uploaded workflow definition for {WorkflowType} with Name={WorkflowName}",
                workflow.WorkflowType,
                workflowName ?? "(null - SG_InputParameters may be disconnected)");
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - allow the agent to continue running
            _logger.LogWarning(ex, "Failed to upload workflow definition for workflow type {WorkflowType}, but continuing. The visualize button may not work until this is resolved.", workflow.WorkflowType);
            
            // Optionally, can uncomment the line below to throw and stop the agent if upload fails
            // throw new InvalidOperationException($"Failed to upload workflow definition for workflow type {workflow.WorkflowType}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts activity definitions from the workflow.
    /// </summary>
    private static ActivityDefinition[] ExtractActivityDefinitions(XiansWorkflow workflow)
    {
        var activities = new List<ActivityDefinition>();

        // Extract activities from user-added activity instances
        foreach (var activityInstance in workflow.GetActivityInstances())
        {
            activities.AddRange(ExtractActivitiesFromType(activityInstance.GetType()));
        }

        // Extract activities from user-added activity types
        foreach (var activityType in workflow.GetActivityTypes())
        {
            activities.AddRange(ExtractActivitiesFromType(activityType));
        }

        // Extract activities from the workflow class itself (if it has activity methods)
        var workflowClassType = workflow.GetWorkflowClassType();
        if (workflowClassType != null)
        {
            activities.AddRange(ExtractActivitiesFromType(workflowClassType));
        }

        return activities.ToArray();
    }

    /// <summary>
    /// Extracts activity definitions from a type by finding methods with [Activity] attributes.
    /// </summary>
    private static List<ActivityDefinition> ExtractActivitiesFromType(Type type)
    {
        var activities = new List<ActivityDefinition>();

        // Find all methods with [Activity] attribute
        var activityMethods = type.GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(Temporalio.Activities.ActivityAttribute), true).Any())
            .ToList();

        foreach (var method in activityMethods)
        {
            var activityAttribute = method.GetCustomAttributes(typeof(Temporalio.Activities.ActivityAttribute), true)
                .FirstOrDefault() as Temporalio.Activities.ActivityAttribute;
            
            var activityName = activityAttribute?.Name ?? method.Name;

            // Extract parameters
            var parameters = method.GetParameters()
                .Select(p => new ParameterDefinition
                {
                    Name = p.Name,
                    Type = p.ParameterType.Name
                })
                .ToList();

            // Extract KnowledgeIds if KnowledgeAttribute is present
            var knowledgeIds = new List<string>();
            try
            {
                var knowledgeAttribute = method.GetCustomAttributes()
                    .FirstOrDefault(attr => attr.GetType().Name == "KnowledgeAttribute");
                
                if (knowledgeAttribute != null)
                {
                    var knowledgeProperty = knowledgeAttribute.GetType().GetProperty("Knowledge");
                    if (knowledgeProperty != null)
                    {
                        var knowledgeValue = knowledgeProperty.GetValue(knowledgeAttribute);
                        if (knowledgeValue is string[] knowledgeArray)
                        {
                            knowledgeIds.AddRange(knowledgeArray);
                        }
                        else if (knowledgeValue is IEnumerable<string> knowledgeEnumerable)
                        {
                            knowledgeIds.AddRange(knowledgeEnumerable);
                        }
                    }
                }
            }
            catch
            {
                // KnowledgeAttribute might not exist in Platform 3, ignore
            }

            activities.Add(new ActivityDefinition
            {
                ActivityName = activityName,
                ParameterDefinitions = parameters,
                KnowledgeIds = knowledgeIds,
                AgentToolNames = new List<string>()
            });
        }

        return activities;
    }

    /// <summary>
    /// Extracts the workflow summary from the [Description] attribute on the workflow class.
    /// </summary>
    private static string? ExtractWorkflowSummary(XiansWorkflow workflow)
    {
        var workflowType = workflow.GetWorkflowClassType();
        if (workflowType == null)
        {
            return null;
        }

        var descAttr = workflowType.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
        return descAttr?.Description;
    }

    /// <summary>
    /// Extracts workflow input parameters from the workflow class using reflection.
    /// </summary>
    private static List<ParameterDefinition> ExtractWorkflowParameters(XiansWorkflow workflow)
    {
        var workflowType = workflow.GetWorkflowClassType();
        if (workflowType == null)
        {
            // Built-in workflows don't have parameters
            return [];
        }

        // Extract parameters from [WorkflowRun] method 
        // This ensures the visualization can properly render SG_InputParameters even for BuiltinWorkflow subclasses
        var workflowRunMethod = workflowType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttributes(typeof(Temporalio.Workflows.WorkflowRunAttribute), true).Any());

        if (workflowRunMethod == null)
        {
            // If no [WorkflowRun] method found, return empty list 
            return [];
        }

        // Extract parameters from the RunAsync method
        return workflowRunMethod.GetParameters()
            .Select(p =>
            {
                var descAttr = p.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
                return new ParameterDefinition
                {
                    Name = p.Name,
                    Type = p.ParameterType.Name,
                    Description = descAttr?.Description,
                    Optional = p.IsOptional
                };
            })
            .ToList();
    }

    /// <summary>
    /// Attempts to read the source code of a type from embedded resources.
    /// Only custom workflows with embedded source files will have source code.
    /// Built-in workflows (dynamic types) do not have real source code.
    /// </summary>
    /// <param name="type">The type to read source for</param>
    /// <returns>The source code, or null if not found</returns>
    private string? ReadSource(Type type)
    {
        try
        {
            // Check if this is a dynamic type (built-in workflow)
            // Dynamic types are created by DynamicWorkflowTypeBuilder via Reflection.Emit
            // Dynamic assemblies have names starting with "DynamicWorkflows_"
            var assemblyName = type.Assembly.GetName().Name;
            var isDynamicAssembly = assemblyName?.StartsWith("DynamicWorkflows_") == true;
            
            if (isDynamicAssembly)
            {
                // Built-in workflows: no real source code exists, return null
                // The Visualize button will be disabled for built-in workflows
                _logger.LogDebug("Skipping source code for built-in workflow {TypeName} (dynamic type)", type.FullName);
                return null;
            }
            else
            {
                // Custom workflows: try to read source from embedded resources (e.g. <EmbeddedResource Include="ConversationalWorkflow.cs" LogicalName="%(Filename)%(Extension)" />)
                var assembly = type.Assembly;
                var candidateName = type.Name + ".cs";
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.Equals(candidateName, StringComparison.OrdinalIgnoreCase) || n.EndsWith("." + candidateName, StringComparison.OrdinalIgnoreCase));
                if (resourceName != null)
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using var reader = new System.IO.StreamReader(stream);
                            var embeddedSource = reader.ReadToEnd();
                            if (!string.IsNullOrWhiteSpace(embeddedSource))
                            {
                                _logger.LogDebug("Found embedded source code for custom workflow {TypeName} from resource {ResourceName}", type.FullName, resourceName);
                                return embeddedSource;
                            }
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading embedded source for {TypeName}", type.FullName);
            return null;
        }
    }

    /// <summary>
    /// Gets a built-in workflow by name.
    /// If name is null, returns the unnamed built-in workflow.
    /// </summary>
    /// <param name="name">The name of the built-in workflow, or null for the unnamed workflow.</param>
    /// <returns>The built-in workflow or null if not found.</returns>
    public XiansWorkflow? GetBuiltIn(string? name = null)
    {
        // Built-in workflows are identified by their WorkflowType containing the agent name
        return _workflows.FirstOrDefault(w => w.Name == name);
    }

    /// <summary>
    /// Gets a custom workflow by its type.
    /// </summary>
    /// <typeparam name="T">The custom workflow type.</typeparam>
    /// <returns>The custom workflow or null if not found.</returns>
    public XiansWorkflow? GetCustom<T>() where T : class
    {
        var workflowType = typeof(T).Name;
        return _workflows.FirstOrDefault(w => w.WorkflowType == workflowType);
    }

    /// <summary>
    /// Gets all workflows for this agent.
    /// </summary>
    /// <returns>A read-only list of workflows.</returns>
    public IReadOnlyList<XiansWorkflow> GetAll()
    {
        return _workflows.AsReadOnly();
    }

    /// <summary>
    /// Gets the workflow type name from the [Workflow] attribute if present.
    /// </summary>
    private string? GetWorkflowTypeFromAttribute<T>(bool validateAgentPrefix) where T : class
    {
        var workflowAttribute = typeof(T).GetCustomAttributes(typeof(Temporalio.Workflows.WorkflowAttribute), false)
            .FirstOrDefault() as Temporalio.Workflows.WorkflowAttribute;

        var workflowType = workflowAttribute?.Name ?? throw new InvalidOperationException($"Workflow type not found for workflow class {typeof(T).Name}");
        
        // Validate that workflow type follows the naming convention (unless it's a platform workflow)
        if (validateAgentPrefix)
        {
            var expectedPrefix = _agent.Name + ":";
            if (!workflowType.StartsWith(expectedPrefix))
            {
                throw new InvalidOperationException(
                    $"Custom workflow type '{workflowType}' must start with agent name prefix '{expectedPrefix}'. " +
                    $"Add [Workflow(\"{expectedPrefix}{workflowType}\")] attribute to your workflow class, " +
                    $"or rename the class to start with the agent name.");
            }
        }

        // must have exactly one ":"
        if (workflowType.Count(c => c == ':') != 1)
        {
            throw new InvalidOperationException($"Workflow type '{workflowType}' must have exactly one ':' in the format of 'AgentName:WorkflowName'");
        }

        return workflowType;
    }

    /// <summary>
    /// Uploads all workflow definitions to the server.
    /// This should be called before running workflows to ensure the server has the latest definitions.
    /// Can also be called explicitly to register the agent without running workflows.
    /// </summary>
    public async Task UploadAllDefinitionsAsync()
    {
        if (_uploader == null)
        {
            return; // No uploader configured, skip
        }

        // Upload agent first (once) before uploading all workflow definitions
        await _uploader.UploadAgentAsync(
            _agent.Name, 
            _agent.SystemScoped, 
            _agent.Description, 
            _agent.Summary,
            _agent.Version, 
            _agent.Author);

        // Upload all workflow definitions
        foreach (var workflow in _workflows)
        {
            await UploadWorkflowDefinitionAsync(workflow);
        }
    }

    /// <summary>
    /// Runs all registered workflows asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for stopping workflows.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task RunAllAsync(CancellationToken cancellationToken)
    {
                
        // Display agent details first
        DisplayAgentDetails(63);
        
        // Display knowledge summary
        await _agent.Knowledge.DisplayKnowledgeSummaryAsync(cancellationToken);
        
        // Upload all workflow definitions to server before starting workers
        // This ensures atomicity - either all workflows are uploaded or none are
        await UploadAllDefinitionsAsync();

        // Display workflow registration summary
        DisplayWorkflowSummary();

        // Run all workflows concurrently with individual error handling
        var tasks = _workflows.Select(async w =>
        {
            try
            {
                await w.RunAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Failed to start workflow '{w.WorkflowType}': {ex.Message}");
                Console.WriteLine($"Exception: {ex}");
                Console.ResetColor();
                throw; // Re-throw to fail the overall operation
            }
        });
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Displays a formatted summary of all registered workflows with their queue names.
    /// </summary>
    private void DisplayWorkflowSummary()
    {
        if (_workflows.Count == 0)
        {
            return;
        }

        // Fixed box width for better console compatibility
        const int boxWidth = 63;
        
        // Display header
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"┌{new string('─', boxWidth)}┐");
        var header = $"│ REGISTERED WORKFLOWS ({_workflows.Count})";
        Console.Write(header);
        Console.WriteLine($"{new string(' ', boxWidth - header.Length + 1)}│");
        Console.WriteLine($"├{new string('─', boxWidth)}┤");
        Console.ResetColor();
        Console.WriteLine();

        // Display each workflow (5 rows per workflow)
        for (int i = 0; i < _workflows.Count; i++)
        {
            var workflow = _workflows[i];
            
            // Agent Name row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Agent:       ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(_agent.Name);
            
            // Workflow Type row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Workflow:    ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(workflow.WorkflowType);
            
            // Task Queue row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Queue:       ");
            Console.ResetColor();
            Console.WriteLine(workflow.TaskQueue);
            
            // System Scoped row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Scope:       ");
            Console.ForegroundColor = _agent.SystemScoped ? ConsoleColor.Magenta : ConsoleColor.Blue;
            Console.WriteLine(_agent.SystemScoped ? "System" : "Tenant");
            
            // Concurrency row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Concurrency: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(workflow.Workers);
            Console.ResetColor();
            
            // Add spacing between workflows (except after the last one)
            if (i < _workflows.Count - 1)
            {
                Console.WriteLine();
            }
        }
        
        // Footer line
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"└{new string('─', boxWidth)}┘");
        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>
    /// Displays formatted agent details.
    /// </summary>
    private void DisplayAgentDetails(int boxWidth)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"┌{new string('─', boxWidth)}┐");
        Console.WriteLine("│ AGENT DETAILS                                                 │");
        Console.WriteLine($"├{new string('─', boxWidth)}┤");
        Console.ResetColor();
        Console.WriteLine();
        
        // Agent Name
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Name            : ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(_agent.Name);
        
        // System Scoped
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Scope           : ");
        Console.ForegroundColor = _agent.SystemScoped ? ConsoleColor.Magenta : ConsoleColor.Blue;
        Console.WriteLine(_agent.SystemScoped ? "System" : "Tenant");
        
        // Description (if available)
        if (!string.IsNullOrEmpty(_agent.Description))
        {
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Description     : ");
            Console.ResetColor();
            var description = _agent.Description.Length > 80 ? _agent.Description.Substring(0, 77) + "..." : _agent.Description;
            Console.WriteLine(description);
        }
        
        // Version (if available)
        if (!string.IsNullOrEmpty(_agent.Version))
        {
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Version         : ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(_agent.Version);
        }
        
        // Author (if available)
        if (!string.IsNullOrEmpty(_agent.Author))
        {
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Author          : ");
            Console.ResetColor();
            Console.WriteLine(_agent.Author);
        }
        
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"└{new string('─', boxWidth)}┘");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();
    }
}

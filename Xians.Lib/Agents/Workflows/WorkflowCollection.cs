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
    /// </summary>
    /// <param name="name">The name for the workflow (e.g., "Conversational", "Web").</param>
    /// <param name="maxConcurrent">Maximum concurrent workflow task executions. Default is 100 (Temporal's default).</param>
    /// <returns>A new built-in XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow with the same name already exists.</exception>
    public XiansWorkflow DefineBuiltIn(string name, int maxConcurrent = 100)
    {
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
            maxConcurrent, 
            isBuiltIn: true, 
            workflowClassType: dynamicWorkflowClassType,
            isPlatformWorkflow: false);
        
        _workflows.Add(workflow);
        
        return workflow;
    }

    /// <summary>
    /// Defines a custom workflow for the agent.
    /// </summary>
    /// <typeparam name="T">The custom workflow type.</typeparam>
    /// <param name="maxConcurrent">Maximum concurrent workflow task executions. Default is 100 (Temporal's default).</param>
    /// <returns>A new custom XiansWorkflow instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a workflow of the same type already exists.</exception>
    public XiansWorkflow DefineCustom<T>(int maxConcurrent = 100) where T : class
    {
        return DefineCustomInternal<T>(maxConcurrent, validateAgentPrefix: true);
    }

    /// <summary>
    /// Internal method to define a custom workflow with optional agent prefix validation.
    /// </summary>
    private XiansWorkflow DefineCustomInternal<T>(int maxConcurrent, bool validateAgentPrefix, bool isPlatformWorkflow = false) where T : class
    {
        // Get workflow type from [Workflow] attribute if present, otherwise use class name
        var workflowType = GetWorkflowTypeFromAttribute<T>(validateAgentPrefix) ?? typeof(T).Name;

        
        // Check if workflow with same type already exists
        if (_workflows.Any(w => w.WorkflowType == workflowType))
        {
            throw new InvalidOperationException($"A workflow of type '{workflowType}' has already been registered.");
        }
        
        var workflow = new XiansWorkflow(_agent, workflowType, null, maxConcurrent, isBuiltIn: false, workflowClassType: typeof(T), isPlatformWorkflow: isPlatformWorkflow);
        _workflows.Add(workflow);
        
        // Note: Workflow definition will be uploaded when RunAllAsync() is called
        
        return workflow;
    }

    /// <summary>
    /// Enables human-in-the-loop (HITL) task support for this agent.
    /// Creates a worker that can handle task assignments requiring human interaction.
    /// </summary>
    /// <param name="maxConcurrent">Maximum concurrent task workflow executions. Default is 100 (Temporal's default).</param>
    /// <returns>The WorkflowCollection instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when task workflow has already been enabled.</exception>
    public async Task<WorkflowCollection> WithTasks(int maxConcurrent = 100)
    {
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
            maxConcurrent, 
            isBuiltIn: false, 
            workflowClassType: dynamicTaskWorkflowType, 
            isPlatformWorkflow: false);
        
        _workflows.Add(workflow);
        
        // Upload the workflow definition to the server immediately
        await UploadWorkflowDefinitionAsync(workflow);
        
        return this;
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
            
            // Extract workflow name from workflow type for custom workflows that extend BuiltinWorkflow
            // Without Name: SG_InputParameters appears disconnected on the side
            // With Name: SG_InputParameters is connected at the top
            // Format: "AgentName:WorkflowName" -> extract "WorkflowName"
            string? workflowName = workflow.Name;
            
            // Always try to extract name if it's null - this is critical for visualization
            if (workflowName == null)
            {
                if (workflowClassType != null)
                {
                    // Check if workflow extends BuiltinWorkflow
                    if (typeof(BuiltinWorkflow).IsAssignableFrom(workflowClassType))
                    {
                        // Extract name from workflow type (e.g., "My Agent:Conversational" -> "Conversational")
                        var workflowTypeParts = workflow.WorkflowType.Split(':', 2);
                        if (workflowTypeParts.Length == 2)
                        {
                            workflowName = workflowTypeParts[1];
                            _logger.LogDebug(
                                "Extracted workflow name '{WorkflowName}' from workflow type '{WorkflowType}' for BuiltinWorkflow subclass",
                                workflowName,
                                workflow.WorkflowType);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Workflow type '{WorkflowType}' does not follow 'AgentName:WorkflowName' format. Name extraction skipped.",
                                workflow.WorkflowType);
                        }
                    }
                }
                else
                {
                    // Try to extract from workflow type even without class type (fallback)
                    var workflowTypeParts = workflow.WorkflowType.Split(':', 2);
                    if (workflowTypeParts.Length == 2)
                    {
                        workflowName = workflowTypeParts[1];
                        _logger.LogDebug(
                            "Extracted workflow name '{WorkflowName}' from workflow type '{WorkflowType}' (no class type available)",
                            workflowName,
                            workflow.WorkflowType);
                    }
                }
            }
            
            // Ensure ParameterDefinitions is always an array (even if empty) for visualization
            var definition = new WorkflowDefinition
            {
                Agent = _agent.Name,
                WorkflowType = workflow.WorkflowType,
                Name = workflowName, // Set name for BuiltinWorkflow subclasses to enable proper visualization
                SystemScoped = _agent.SystemScoped,
                Workers = (workflow.Workers == 1) ? 1 : workflow.Workers, // Keep default (1) 
                ActivityDefinitions = activities,
                ParameterDefinitions = parameters, // Always an array (empty [] if no parameters)
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
            .Select(p => new ParameterDefinition
            {
                Name = p.Name,
                Type = p.ParameterType.Name
            })
            .ToList();
    }

    /// <summary>
    /// Attempts to read the source code of a type from embedded resources or generated source cache.
    /// </summary>
    /// <param name="type">The type to read source for</param>
    /// <returns>The source code, or null if not found</returns>
    private string? ReadSource(Type type)
    {
        try
        {
            // First, check if this is a dynamic type with generated source code
            // Dynamic types are created by DynamicWorkflowTypeBuilder and have generated source
            // Check if the assembly is a dynamic assembly (created via Reflection.Emit)
            // Dynamic assemblies have names starting with "DynamicWorkflows_"
            var assemblyName = type.Assembly.GetName().Name;
            var isDynamicAssembly = assemblyName?.StartsWith("DynamicWorkflows_") == true;
            
            if (isDynamicAssembly)
            {
                // Try to get the workflow type name from the [Workflow] attribute
                var workflowAttribute = type.GetCustomAttribute<Temporalio.Workflows.WorkflowAttribute>();
                if (workflowAttribute != null && !string.IsNullOrEmpty(workflowAttribute.Name))
                {
                    var generatedSource = DynamicWorkflowTypeBuilder.GetSourceCode(workflowAttribute.Name);
                    if (!string.IsNullOrWhiteSpace(generatedSource))
                    {
                        _logger.LogDebug("Found generated source code for dynamic type {TypeName} (workflow: {WorkflowType})", type.FullName, workflowAttribute.Name);
                        return generatedSource;
                    }
                }
            }

            // Fall back to reading from embedded resources (for DefineCustom workflows)
            var assembly = type.Assembly;
            var resourceName = $"{type.Name}.cs";
            // assemblyName already declared above, reuse it

            // Try multiple resource name patterns (handles both with and without LogicalName)
            var possibleResourceNames = new List<string>
            {
                resourceName, // e.g., "ConversationalWorkflow.cs" (when using LogicalName)
                $"{assemblyName}.{resourceName}", // e.g., "MyAgent.ConversationalWorkflow.cs" (default behavior)
            };

            // If type has namespace, try with namespace prefix
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                var namespacePrefix = type.Namespace.Replace(".", ".");
                possibleResourceNames.Add($"{assemblyName}.{namespacePrefix}.{resourceName}");
            }

            Stream? stream = null;
            string? foundResourceName = null;

            foreach (var name in possibleResourceNames)
            {
                stream = assembly.GetManifestResourceStream(name);
                if (stream != null)
                {
                    foundResourceName = name;
                    break;
                }
            }

            if (stream == null)
            {
                _logger.LogWarning("Source code not found in assembly '{AssemblyName}' for type '{TypeName}'. Make sure the workflow's .cs file is marked as an EmbeddedResource in your .csproj file.", assemblyName, type.FullName);
                return null;
            }

            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(source))
            {
                _logger.LogWarning("Source code found for {TypeName} but it is empty or whitespace", type.FullName);
                return null;
            }

            _logger.LogDebug("Found source code for {TypeName} in resource '{ResourceName}' ({Length} characters)", type.FullName, foundResourceName, source.Length);
            return source;
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

using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.Activity;
using XiansAi.Flow;
using Server.Http;
using XiansAi.Models;

namespace Server;

/// <summary>
/// Defines a service for uploading flow definitions to the server.
/// </summary>
public interface IFlowDefinitionUploader
{
    /// <summary>
    /// Uploads a flow definition to the server.
    /// </summary>
    /// <typeparam name="TFlow">The type of flow to upload</typeparam>
    /// <param name="flow">The flow information</param>
    /// <param name="source">Optional source code of the flow</param>
    /// <returns>A task representing the upload operation</returns>
    Task UploadFlowDefinition<TFlow>(FlowInfo<TFlow> flow, string? source = null) 
        where TFlow : class;
}

/// <summary>
/// Implementation of the flow definition uploader that can send flow definitions to the server.
/// </summary>
public class FlowDefinitionUploader : IFlowDefinitionUploader
{
    private readonly ILogger<FlowDefinitionUploader> _logger;
    private readonly ISecureApiClient _secureApi;
    
    private const string API_ENDPOINT = "api/agent/definitions";

    /// <summary>
    /// Initializes a new instance of the FlowDefinitionUploader class.
    /// </summary>
    /// <param name="loggerFactory">Factory to create a logger instance</param>
    /// <param name="secureApi">Secure API client for server communication</param>
    /// <exception cref="ArgumentNullException">Thrown if secureApi is null</exception>
    public FlowDefinitionUploader()
    {
        _logger = Globals.LogFactory.CreateLogger<FlowDefinitionUploader>();
        _secureApi = SecureApi.Instance;
    }

    /// <summary>
    /// Uploads a flow definition to the server.
    /// </summary>
    /// <typeparam name="TFlow">The type of flow to upload</typeparam>
    /// <param name="flow">The flow information</param>
    /// <param name="source">Optional source code of the flow</param>
    /// <returns>A task representing the upload operation</returns>
    /// <exception cref="InvalidOperationException">Thrown if upload fails</exception>
    public async Task UploadFlowDefinition<TFlow>(FlowInfo<TFlow> flow, string? source = null)
        where TFlow : class
    {
        var flowDefinition = CreateFlowDefinition(flow, source);
        _logger.LogInformation("Uploading flow definition of {TypeName} to App server...", typeof(TFlow).FullName);
        
        await UploadToServer(flowDefinition);
    }

    /// <summary>
    /// Creates a flow definition from the provided flow information.
    /// </summary>
    /// <typeparam name="TFlow">The type of flow</typeparam>
    /// <param name="flow">The flow information</param>
    /// <param name="source">Optional source code</param>
    /// <returns>A flow definition ready to be uploaded</returns>
    private FlowDefinition CreateFlowDefinition<TFlow>(FlowInfo<TFlow> flow, string? source)
        where TFlow : class
    {
        return new FlowDefinition {
            TypeName = flow.GetWorkflowName(),
            AgentName = flow.GetAgentName(),
            Parameters = flow.GetParameters(),
            Activities = GetAllActivities(flow.GetObjects()).ToArray(),
            Source = source ?? ReadSource(typeof(TFlow)),
            Categories = flow.GetCategories()
        };
    }

    /// <summary>
    /// Uploads a flow definition to the server.
    /// </summary>
    /// <param name="flowDefinition">The flow definition to upload</param>
    /// <returns>A task representing the upload operation</returns>
    /// <exception cref="InvalidOperationException">Thrown if server connection fails or returns an error</exception>
    private async Task UploadToServer(FlowDefinition flowDefinition)
    {
        if (!_secureApi.IsReady)
        {
            _logger.LogError("App server secure API is not available, upload of flow definition failed");
            throw new InvalidOperationException("App server secure API is not available");
        }
        
        try
        {
            var client = _secureApi.Client;
            var response = await client.PostAsync(API_ENDPOINT, JsonContent.Create(flowDefinition));
            
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                _logger.LogError("Bad Request: {ErrorMessage}", errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Flow definition uploaded successfully: {ResponseBody}", responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to upload flow definition: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to upload flow definition", ex);
        }
    }

    /// <summary>
    /// Attempts to read the source code of a type from embedded resources.
    /// </summary>
    /// <param name="type">The type to read source for</param>
    /// <returns>The source code, or null if not found</returns>
    private string? ReadSource(Type type)
    {
        try
        {
            var assembly = type.Assembly;
            var resourceName = $"{type.Name}.cs";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger.LogWarning("Source code not found in assembly. Did you forget to embed `{TypeName}.cs` to the project?", type.FullName);
                return null;
            }

            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();
            _logger.LogDebug("Found source code for {TypeName} in resource {ResourceName}", 
                type.FullName, resourceName);
            return source;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading embedded source for {TypeName}", type.FullName);
            return null;
        }
    }

    /// <summary>
    /// Gets all activities from a list of flow objects.
    /// </summary>
    /// <param name="objects">List of flow objects</param>
    /// <returns>List of activity definitions</returns>
    private List<ActivityDefinition> GetAllActivities(List<(Type interfaceType, object stub, object proxy)> objects) 
    {
        var activities = new List<ActivityDefinition>();
        
        foreach (var (interfaceType, _, _) in objects) 
        {
            var agentToolsAttribute = interfaceType.GetCustomAttributes<AgentToolAttribute>();
            var agentToolNames = agentToolsAttribute?.Select(a => a.ToString() ?? "").ToList() ?? [];
            activities.AddRange(GetActivities(interfaceType, agentToolNames));
        }
        
        return activities;
    }

    /// <summary>
    /// Extracts activity definitions from a type.
    /// </summary>
    /// <param name="stubType">The type to extract activities from</param>
    /// <param name="agentNames">List of agent tool names</param>
    /// <returns>List of activity definitions</returns>
    private List<ActivityDefinition> GetActivities(Type stubType, List<string> agentNames) 
    {
        var activityMethods = stubType.GetMethods()
            .Where(m => m.GetCustomAttribute<Temporalio.Activities.ActivityAttribute>() != null)
            .ToList();

        var activities = new List<ActivityDefinition>();

        foreach (var method in activityMethods) 
        {
            var activityAttribute = method.GetCustomAttribute<Temporalio.Activities.ActivityAttribute>();
            var activityName = activityAttribute?.Name ?? method.Name;
            
            var parameters = method.GetParameters()
                .Select(p => new ParameterDefinition 
                {
                    Name = p.Name,
                    Type = p.ParameterType.Name
                })
                .ToList();

            var knowledgeAttribute = method.GetCustomAttribute<KnowledgeAttribute>();

            activities.Add(new ActivityDefinition 
            {
                ActivityName = activityName,
                Parameters = parameters,
                Instructions = knowledgeAttribute?.Knowledge.ToList() ?? [],
                AgentToolNames = agentNames
            });
        }
        
        return activities;
    }
}
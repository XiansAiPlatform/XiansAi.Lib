using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiansAi.Flow;
using XiansAi.Knowledge;
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
    Task UploadFlowDefinition<TFlow>(Runner<TFlow> flow,  RunnerOptions? options) 
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
    public async Task UploadFlowDefinition<TFlow>(Runner<TFlow> flow,  RunnerOptions? options)
        where TFlow : class
    {
        
        var flowDefinition = CreateFlowDefinition(flow, options);
        _logger.LogDebug("Uploading flow definition of {TypeName} to App server...", typeof(TFlow).FullName);
        
        await UploadToServer(flowDefinition);
    }

    /// <summary>
    /// Creates a flow definition from the provided flow information.
    /// </summary>
    /// <typeparam name="TFlow">The type of flow</typeparam>
    /// <param name="flow">The flow information</param>
    /// <param name="source">Optional source code</param>
    /// <returns>A flow definition ready to be uploaded</returns>
    private FlowDefinition CreateFlowDefinition<TFlow>(Runner<TFlow> flow, RunnerOptions? options)
        where TFlow : class
    {
        var systemScoped = options?.SystemScoped ?? false;
        var onboardingJson = options?.OnboardingJson;
        return new FlowDefinition {
            WorkflowType = flow.WorkflowName,
            Agent = flow.AgentName,
            ParameterDefinitions = flow.WorkflowParameters,
            ActivityDefinitions = GetAllActivities(flow.ActivityInterfaces).ToArray(),
            Source = ReadSource(typeof(TFlow)),
            SystemScoped = systemScoped,
            OnboardingJson = onboardingJson
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
        if (!SecureApi.IsReady)
        {
            _logger.LogError("App server secure API is not available, upload of flow definition failed");
            throw new InvalidOperationException("App server secure API is not available");
        }
        
        try
        {
            var client = _secureApi.Client;
            string serializedDefinition = JsonSerializer.Serialize(flowDefinition);
            string hash = ComputeHash(serializedDefinition);

            bool systemScoped = flowDefinition.SystemScoped;

            string checkUrl = $"{API_ENDPOINT}/check?workflowType={flowDefinition.WorkflowType}&systemScoped={systemScoped}&hash={hash}";
            var hashCheckResponse = await client.GetAsync(checkUrl);

            switch (hashCheckResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                    _logger.LogInformation("Flow definition for {TypeName} already up to date on server", flowDefinition.WorkflowType);
                    return;

                case HttpStatusCode.NotFound:
                    // Proceed with upload
                    break;

                default:
                    string error = await hashCheckResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Hash check failed with status {StatusCode}: {Error}", hashCheckResponse.StatusCode, error);
                    throw new InvalidOperationException($"Hash check failed: {error}");
            }

            var response = await client.PostAsync(API_ENDPOINT, JsonContent.Create(flowDefinition));
            
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                _logger.LogError("Bad Request: {ErrorMessage}", errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            
            // Handle permission warning (403 Forbidden)
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var warningJson = await response.Content.ReadAsStringAsync();
                try {
                    var warningResponse = System.Text.Json.JsonSerializer.Deserialize<WarningResponse>(warningJson);
                    _logger.LogWarning("Permission warning: {Message}", warningResponse?.message);
                    throw new InvalidOperationException(warningResponse?.message ?? "Permission denied for this flow definition");

                } 
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing warning response: {ErrorMessage}", ex.Message);
                    throw new InvalidOperationException("Error deserializing warning response. Is the server running on the correct port?", ex);
                }
            }
            
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Flow definition for {TypeName} uploaded successfully: {ResponseBody}", flowDefinition.WorkflowType, responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to upload flow definition: {Message}", ex.Message);
            throw new InvalidOperationException("Failed to upload flow definition", ex);
        }
    }

    private string ComputeHash(string source)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(source);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLower();
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
    /// <param name="interfaceTypes">List of interface types</param>
    /// <returns>List of activity definitions</returns>
    private List<ActivityDefinition> GetAllActivities(List<Type> interfaceTypes) 
    {
        var activities = new List<ActivityDefinition>();
        
        foreach (var interfaceType in interfaceTypes) 
        {
            activities.AddRange(GetActivities(interfaceType));
        }
        
        return activities;
    }

    /// <summary>
    /// Extracts activity definitions from a type.
    /// </summary>
    /// <param name="stubType">The type to extract activities from</param>
    /// <param name="agentNames">List of agent tool names</param>
    /// <returns>List of activity definitions</returns>
    private List<ActivityDefinition> GetActivities(Type stubType) 
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
                ParameterDefinitions = parameters,
                KnowledgeIds = knowledgeAttribute?.Knowledge.ToList() ?? [],
                AgentToolNames = new List<string>()
            });
        }
        
        return activities;
    }
    

    /// <summary>
    /// Class to deserialize warning messages from the server
    /// </summary>
    internal class WarningResponse
    {
        public string? message { get; set; }
    }
}
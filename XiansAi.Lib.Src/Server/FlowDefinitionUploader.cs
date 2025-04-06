using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.Activity;
using XiansAi.Flow;
using Server.Http;
using XiansAi.Models;

namespace Server;

public class FlowDefinitionUploader
{
    private readonly ILogger<FlowDefinitionUploader> _logger;

    public FlowDefinitionUploader()
    {
        _logger = Globals.LogFactory.CreateLogger<FlowDefinitionUploader>();
    }

    public async Task UploadFlowDefinition<TFlow>(FlowInfo<TFlow> flow, string? source = null)
        where TFlow : class
    {
        var flowDefinition = new FlowDefinition {
            TypeName = flow.GetWorkflowName(),
            AgentName = flow.GetAgentName(),
            Parameters = flow.GetParameters(),
            Activities = GetAllActivities(flow.GetObjects()).ToArray(),
            Source = source ?? ReadSource(typeof(TFlow)),
            Categories = flow.GetCategories()
        };
        _logger.LogInformation("Uploading flow definition of {TypeName} to App server...", typeof(TFlow).FullName);
        await Upload(flowDefinition);
    }

    private async Task Upload(FlowDefinition flowDefinition)
    {
        if (SecureApi.Instance.IsReady)
        {
            try
            {
                var client = SecureApi.Instance.Client;
                var response = await client.PostAsync("api/agent/definitions", JsonContent.Create(flowDefinition));
                
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
                _logger.LogError("Failed to upload flow definition: {Message}", ex.Message);
                throw new InvalidOperationException("Failed to upload flow definition", ex);
            }
        } 
        else
        {
            _logger.LogError("App server secure API to server is not available, upload of flow definition failed");
        }
    }

    private string? ReadSource(Type type)
    {
        try
        {
            var assembly = type.Assembly;
            var resourceName = $"{type.Name}.cs";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                // Debug helper: List all available resources
                var resources = assembly.GetManifestResourceNames();
                _logger.LogWarning("Source code not found in assembly. Did you forget to embed `{TypeName}.cs` to the project?",  type.FullName);
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

    private List<ActivityDefinition> GetAllActivities(List<(Type interfaceType, object stub, object proxy)> objects) {
        var activities = new List<ActivityDefinition>();
        foreach (var (interfaceType, stub, proxy) in objects) {
            var agentToolsAttribute = interfaceType.GetCustomAttributes<AgentToolAttribute>();
            var agentToolNames = agentToolsAttribute?.Select(a => a.ToString() ?? "").ToList() ?? [];
            activities.AddRange(GetActivities(interfaceType, agentToolNames));
        }
        return activities;
    }

    private List<ActivityDefinition> GetActivities(Type stubType, List<string> agentNames) {
        var activityMethods = stubType.GetMethods().Where(m => m.GetCustomAttribute<Temporalio.Activities.ActivityAttribute>() != null).ToList();

        var activities = new List<ActivityDefinition>();

        foreach (var method in activityMethods) {
            var activityName = method.GetCustomAttribute<Temporalio.Activities.ActivityAttribute>()?.Name ?? method.Name;
            var parameters = method.GetParameters().Select(p => new ParameterDefinition {
                Name = p.Name,
                Type = p.ParameterType.Name
            }).ToList() ?? [];

            var knowledgeAttribute = method.GetCustomAttribute<KnowledgeAttribute>();

            activities.Add(new ActivityDefinition {
                ActivityName = activityName,
                Parameters = parameters,
                Instructions = knowledgeAttribute?.Knowledge.ToList() ?? [],
                AgentToolNames = agentNames
            });
        }
        return activities;
    }

}
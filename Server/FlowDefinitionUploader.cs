using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiansAi.Activity;
using XiansAi.Flow;
using XiansAi.Http;
using XiansAi.Models;

namespace XiansAi.Server;

public class FlowDefinitionUploader
{
    private readonly ILogger<FlowDefinitionUploader> _logger;

    public FlowDefinitionUploader()
    {
        _logger = Globals.LogFactory.CreateLogger<FlowDefinitionUploader>();
    }

    public async Task UploadFlowDefinition<TFlow>(FlowInfo<TFlow> flow)
        where TFlow : class
    {
        var flowDefinition = new FlowDefinition {
            TypeName = flow.GetWorkflowName(),
            ClassName = typeof(TFlow).Name,
            Parameters = flow.GetParameters().Select(p => new ParameterDefinition {
                Name = p.Name,
                Type = p.ParameterType.Name
            }).ToList(),
            Activities = flow.GetActivities().Select(CreateActivityDefinition).ToArray(),
            Source = ReadSource(typeof(TFlow))
        };

        _logger.LogInformation("Trying to upload flow definition: {FlowDefinition}", JsonSerializer.Serialize(flowDefinition));

        await Upload(flowDefinition);

    }

    private async Task Upload(FlowDefinition flowDefinition)
    {
        if (SecureApi.IsReady())
        {
            HttpClient client = SecureApi.GetClient();
            var response = await client.PostAsync("api/server/flow-definitions", JsonContent.Create(flowDefinition));
            response.EnsureSuccessStatusCode();
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
                _logger.LogWarning("Source code for {TypeName} not found. Available resources: {Resources}", 
                    type.FullName, string.Join(", ", resources));
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

    private ActivityDefinition CreateActivityDefinition(KeyValuePair<Type, object> activity)
    {
        var dockerImageAttribute = activity.Value.GetType().GetCustomAttribute<DockerImageAttribute>();
        var instructionsAttribute = activity.Value.GetType().GetCustomAttribute<InstructionsAttribute>();

        return new ActivityDefinition {
            Instructions = instructionsAttribute?.Instructions.ToList() ?? [],
            DockerImage = dockerImageAttribute?.Name,
            ActivityName = activity.Key.Name,
            Parameters = activity.Key.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<Temporalio.Activities.ActivityAttribute>() != null)?
                .GetParameters().Select(p => new ParameterDefinition {
                    Name = p.Name,
                    Type = p.ParameterType.Name
                }).ToList() ?? []
        };
    }
}
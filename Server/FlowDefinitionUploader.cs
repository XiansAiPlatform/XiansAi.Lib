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

    public async Task UploadFlowDefinition<TFlow>(FlowInfo<TFlow> flow, string? source = null)
        where TFlow : class
    {
        var flowDefinition = new FlowDefinition {
            TypeName = flow.GetWorkflowName(),
            ClassName = typeof(TFlow).FullName ?? typeof(TFlow).Name,
            Parameters = flow.GetParameters().Select(p => new ParameterDefinition {
                Name = p.Name,
                Type = p.ParameterType.Name
            }).ToList(),
            Activities = flow.GetActivities().Select(CreateActivityDefinition).ToArray(),
            Source = source ?? ReadSource(typeof(TFlow))
        };
        await Task.Delay(1000);
        _logger.LogInformation("Uploading flow definition for {TypeName} to App server...", typeof(TFlow).FullName);
        await Upload(flowDefinition);
    }

    private async Task Upload(FlowDefinition flowDefinition)
    {
        if (SecureApi.IsReady())
        {
            try
            {
                HttpClient client = SecureApi.GetClient();
                var response = await client.PostAsync("api/server/definitions", JsonContent.Create(flowDefinition));
                
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
            _logger.LogWarning("App server secure API is not ready, skipping upload of flow definition");
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

    private ActivityDefinition CreateActivityDefinition(KeyValuePair<Type, object> activity)
    {
        var agentsAttribute = activity.Value.GetType().GetCustomAttribute<AgentsAttribute>();
        var instructionsAttribute = activity.Value.GetType().GetCustomAttribute<InstructionsAttribute>();

        return new ActivityDefinition {
            Instructions = instructionsAttribute?.Instructions.ToList() ?? [],
            Agents = agentsAttribute?.Names.ToList() ?? [],
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
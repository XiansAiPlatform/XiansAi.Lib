using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Temporalio.Workflows;
using XiansAi.Activity;
using XiansAi.Http;
using XiansAi.Models;
using XiansAi.Temporal;

namespace XiansAi.Flow;

public interface IFlowRunnerService
{
    Task RunFlowAsync<TFlow>(FlowInfo<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class;
}

public class FlowRunnerService : IFlowRunnerService
{
    private readonly TemporalClientService _temporalClientService;
    private readonly TemporalConfig _temporalConfig;
    private readonly ILogger<FlowRunnerService> _logger;
    public FlowRunnerService(TemporalConfig temporalConfig, XiansAIConfig xiansAIConfig)
    {
        _logger = Globals.LogFactory.CreateLogger<FlowRunnerService>();
        _temporalConfig = temporalConfig;
        _temporalClientService = new TemporalClientService(_temporalConfig);
        if (xiansAIConfig.CertificatePath != null && xiansAIConfig.CertificatePassword != null && xiansAIConfig.ServerUrl != null) {
            SecureApi.Initialize(
                xiansAIConfig.CertificatePath,
                xiansAIConfig.CertificatePassword,
                xiansAIConfig.ServerUrl
            );
        }
    }

    private string GetWorkflowName<TFlow>() where TFlow : class
    {
        var workflowAttr = typeof(TFlow).GetCustomAttribute<WorkflowAttribute>();
        if (workflowAttr == null)
        {
            throw new InvalidOperationException($"Workflow {typeof(TFlow).Name} is missing WorkflowAttribute");
        }
        return workflowAttr.Name ?? typeof(TFlow).Name;
    }

    public async Task RunFlowAsync<TFlow>(FlowInfo<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class
    {
        // Upload the flow definition to the server
        UploadFlowDefinition(flow);

        // Run the worker for the flow
        var client = await _temporalClientService.GetClientAsync();
        var workFlowName = GetWorkflowName<TFlow>();
        
        var options = new TemporalWorkerOptions(taskQueue: workFlowName.Replace(" ", ""));
        options.AddWorkflow<TFlow>();
        foreach (var activity in flow.GetProxyActivities())
        {
            options.AddAllActivities(activity.Key, activity.Value);
        }

        var worker = new TemporalWorker(
            client,
            options
        );

        await worker.ExecuteAsync(cancellationToken);
    }

    public void UploadFlowDefinition<TFlow>(FlowInfo<TFlow> flow)
    {
        var flowDefinition = new FlowDefinition {
            TypeName = flow.GetWorkflowName(),
            ClassName = typeof(TFlow).Name,
            Parameters = flow.GetParameters().Select(p => new ParameterDefinition {
                Name = p.Name,
                Type = p.ParameterType.Name
            }).ToList(),
            Activities = flow.GetActivities().Select(CreateActivityDefinition).ToArray()
        };

        _logger.LogInformation("Flow definition: {FlowDefinition}", JsonSerializer.Serialize(flowDefinition));
    }

    private ActivityDefinition CreateActivityDefinition(KeyValuePair<Type, object> activity)
    {
        Console.WriteLine(activity.Key.Name);
        Console.WriteLine(activity.Value.GetType().Name);

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

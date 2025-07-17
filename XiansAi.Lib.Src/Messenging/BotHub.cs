using Microsoft.Extensions.Logging;
using Temporal;
using Temporalio.Client;
using XiansAi.Messaging;

public class BotHub
{

    private static readonly ILogger<BotHub> _logger = Globals.LogFactory.CreateLogger<BotHub>();
    public static async Task<string> SendChat(MessageThread messageThread, Type botClassType)
    {

        var request = new MessageSignal
        {
            SourceAgent = AgentContext.AgentName,
            SourceWorkflowId = AgentContext.WorkflowId,
            SourceWorkflowType = AgentContext.WorkflowType,

            Payload = new MessagePayload{
                 Agent = messageThread.Agent,
                 ThreadId = messageThread.ThreadId ?? throw new InvalidOperationException("Thread ID is required"),
                 ParticipantId = messageThread.ParticipantId,
                 Type = messageThread.LatestMessage.Type.ToString(),
                 Text = messageThread.LatestMessage.Content ?? throw new InvalidOperationException("Message content is required"), 
                 RequestId = messageThread.LatestMessage.RequestId,
                 Scope = messageThread.LatestMessage.Scope,
                 Hint = messageThread.LatestMessage.Hint,
                 Data = messageThread.LatestMessage.Data ?? new object(),
                 Authorization = messageThread.Authorization
            }
        };


        _logger.LogInformation($"Sending chat to workflow {botClassType} with request {request}");
        var response = await HandleTemporalUpdate(request, botClassType);

        _logger.LogInformation($"Received response from workflow {botClassType} with response {response}");

        return response;
    }


    private static async Task<string> HandleTemporalUpdate(MessageSignal request, Type botClassType)
    {
        var procedureName = Constants.UPDATE_INBOUND_CHAT_OR_DATA;

        var workflowId = AgentContext.GetSingletonWorkflowIdFor(botClassType);
        var workflowType = AgentContext.GetWorkflowTypeFor(botClassType);

        if (workflowId == null)
        {
            throw new InvalidOperationException("Workflow type is required to send chat");
        }

        if (workflowId == null)
        {
            workflowId = AgentContext.TenantId + ":" + workflowType;
        }
        else if (!workflowId.StartsWith(AgentContext.TenantId + ":"))
        {
            throw new InvalidOperationException("Workflow ID must start with tenant ID");
        }

        try
        {
            var client = TemporalClientService.Instance.GetClientAsync();
            // singleton workflow id
            var workflowHandle = client.GetWorkflowHandle(workflowId);

            var workflowOptions = new NewWorkflowOptions(workflowType, workflowId);

            var withStartWorkflowOperation = WithStartWorkflowOperation.Create(
                workflowType,
                [],
                workflowOptions
            );

            var workflowUpdateWithStartOptions = new WorkflowUpdateWithStartOptions(
                withStartWorkflowOperation
            );


            var response = await client.ExecuteUpdateWithStartWorkflowAsync<BotResponse>(
                procedureName,
                [request],
                workflowUpdateWithStartOptions
                );

            return response.Text ?? throw new InvalidOperationException("Bot response text is null");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            throw;
        }
    }
}

public class BotResponse
{
    public string? Text { get; set; }
    public object? Data { get; set; }
    public string? RequestId { get; set; }
    public string? Scope { get; set; }
    public string? ParticipantId { get; set; }
    public string? WorkflowId { get; set; }
    public string? WorkflowType { get; set; }
    public required string Agent { get; set; }
    public string? Authorization { get; set; }
    public string? ThreadId { get; set; }
    public bool IsComplete { get; set; } = true;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
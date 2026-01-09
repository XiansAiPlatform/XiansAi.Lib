using Microsoft.Agents.AI;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Temporal.Workflows.Messaging.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents.Core;
using Xians.Agent.Sample.Utils;
using Xians.Agent.Sample.SupervisorAgent;
using Microsoft.Extensions.Logging;

namespace Xians.Agent.Sample;

/// <summary>
/// MAF Agent that uses OpenAI with Xians chat message store for conversation history.
/// </summary>
internal class ConversationalAgent
{
    private static readonly ILogger _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.Instance.CreateLogger("ConversationalAgent");
    
    private readonly ChatClient _chatClient;

    /// <summary>
    /// Initializes a new instance of the ConversationalAgent.
    /// </summary>
    /// <param name="openAiApiKey">OpenAI API key for authentication</param>
    /// <param name="modelName">OpenAI model to use (defaults to gpt-4o-mini)</param>
    public ConversationalAgent(string openAiApiKey, string modelName = "gpt-4o-mini")
    {
        _chatClient = new OpenAIClient(openAiApiKey).GetChatClient(modelName);
    }

    /// <summary>
    /// Processes a user message using OpenAI's chat model with Xians conversation history.
    /// </summary>
    /// <param name="context">The Xians user message context containing the message and chat history</param>
    /// <returns>The AI agent's response text</returns>
    public async Task<string> ProcessMessageAsync(UserMessageContext context)
    {
        var taskWorkflowId = await context.GetLastHintAsync();
        _logger.LogInformation("Task workflow ID: {TaskWorkflowId}", taskWorkflowId);

        // Create context-specific tools
        var webTools = new WebTools(context);
        var taskTools = new TaskTools(context);

        // Create AI agent with custom Xians chat message store and tools
        AIAgent mafAgent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    Instructions = @"You are a human-in-the-loop task management assistant. Your responsibilities include:
                        1. Managing tasks that require human approval or rejection
                        2. Using task tools to understand task details and draft content
                        3. Working collaboratively with users to review and update draft content
                        4. Facilitating task approvals and rejections
                        5. Proactively identifying pending tasks in the conversation history and prompting users to review and approve/reject them along with their draft content
                        
                        CRITICAL: At the start of EVERY user interaction, you MUST execute GetTaskInfo to check for pending tasks requiring approval. 
                        If a pending task exists, immediately present it to the user with its draft content and ACTIVELY ENCOURAGE them to work on it.
                        Drive the conversation towards task completion by:
                        - Presenting the task details and draft content clearly
                        - Highlighting what needs to be decided
                        - Actively encouraging the user to approve or reject the task
                        - Asking direct questions to facilitate decision-making
                        - Being persistent but respectful in guiding them to complete the task
                        
                        Always prioritize resolving open tasks before addressing other topics.",
                    Tools =
                    [
                        AIFunctionFactory.Create(webTools.ResearchCompany),
                        AIFunctionFactory.Create(taskTools.ApproveTask),
                        AIFunctionFactory.Create(taskTools.RejectTask),
                        AIFunctionFactory.Create(taskTools.GetTaskInfo),
                        AIFunctionFactory.Create(taskTools.GetTaskDraft),
                        AIFunctionFactory.Create(taskTools.UpdateTaskDraft),
                    ]
                },
                ChatMessageStoreFactory = ctx =>
                {
                    // Create a new chat message store that reads from Xians platform
                    return new XiansChatMessageStore(
                        context,
                        ctx.SerializedState,
                        ctx.JsonSerializerOptions);
                }
            });

        var response = await mafAgent.RunAsync(context.Message.Text);
        return response.Text;
    }
}


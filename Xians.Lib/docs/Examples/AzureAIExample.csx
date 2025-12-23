// dotnet add package Azure.AI.Projects --version 1.2.*-*
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using OpenAI;
using OpenAI.Responses;

// Xians
using Xians.Lib;
using Xians.Lib.Agents;

#pragma warning disable OPENAI001


const string projectEndpoint = "https://hasithy-2369-resource.services.ai.azure.com/api/projects/hasithy-2369";
const string modelDeploymentName = "gpt-4o-mini";
const string agentName = "CRM Data Wash Agent";


// Connect to your project using the endpoint from your project page
// The AzureCliCredential will use your logged-in Azure CLI identity, make sure to run `az login` first
AIProjectClient projectClient = new(endpoint: new Uri(projectEndpoint), tokenProvider: new DefaultAzureCredential());

// Create your agent
PromptAgentDefinition agentDefinition = new(model: modelDeploymentName)
{
    Instructions = "You are a storytelling agent. You craft engaging one-line stories based on user prompts and context.",
};

// Creates an agent or bumps the existing agent version if parameters have changed
AgentVersion agentVersion = projectClient.Agents.CreateAgentVersion(
    agentName: agentName,
    options: new(agentDefinition));

OpenAIResponseClient responseClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentVersion);

XiansPlatform xians = XiansPlatform.Initialize(new XiansOptions
{
    ServerUrl = "https://api.example.com",
    ApiKey = "your-api-key-here"
});

// Register the agent
XiansAgent xiansAgent = xians.Agents.Register(new XiansAgentRegistration
{
    Name = agentVersion.Name,
    SystemScoped = false
});

// Define the default workflow
XiansWorkflow defaultWorkflow = await xiansAgent.Workflows.DefineBuiltIn(workers: 1, name: "Conversational");

// On user message, generate a response
defaultWorkflow.OnUserMessage(async context =>
    {
        string message = context.Message.Text;

        // Use the agent to generate a response
        OpenAIResponse response = responseClient.CreateResponse(message);

        Console.WriteLine(response.GetOutputText());

        context.ReplyBegin(response);

        for () {
            context.ReplyPartial(response);
        }
        
        context.ReplyEnd(response);
    });

// Define the custom data wash workflow by specifying the workflow class 
XiansWorkflow dataWashWorkflow = await xiansAgent.Workflows.DefineCustom<DataWashWorkflow>(workers: 1);

// Run the workflows
await xiansAgent.RunAllAsync();


using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;

namespace Xians.Examples.MultiAgentOrchastration.Validator;

/// <summary>
/// Registers the Validator Agent and sets up its workflows.
/// This agent validates the data and structure of invoices.
/// </summary>
public static class ValidatorAgent
{
    public static XiansAgent Setup(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new()
        {
            Name = "Validator Agent",
            Author = "Xians",
            Description = "Validator Agent is a agent that validates the invoice processing workflow.",
            Version = "1.0.0",
            Category = "Validation",
            IsTemplate = true,
        });

        agent.Workflows.DefineCustom<ValidatorWorkflow>(new WorkflowOptions { Activable = false });

        return agent;
    }
}

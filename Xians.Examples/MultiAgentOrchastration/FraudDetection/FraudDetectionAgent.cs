using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;

namespace Xians.Examples.MultiAgentOrchastration.FraudDetection;

/// <summary>
/// Registers the Fraud Detection Agent and sets up its workflows.
/// This agent analyzes invoices for signs of fraud.
/// </summary>
public static class FraudDetectionAgent
{
    public static XiansAgent Setup(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new()
        {
            Name = "Fraud Detection Agent",
            Author = "Xians",
            Description = "Fraud Detection Agent is a agent that detects fraud in the invoice processing workflow.",
            Version = "1.0.0",
            Category = "Fraud",
            IsTemplate = true,
        });

        agent.Workflows.DefineCustom<FraudDetectionWorkflow>(new WorkflowOptions { Activable = false });

        return agent;
    }
}

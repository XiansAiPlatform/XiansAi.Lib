using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;

namespace Xians.Examples.MultiAgentOrchastration.InvoiceProcessor;

/// <summary>
/// Registers the Invoice Processor Agent and sets up its workflows.
/// This agent carries out the actual processing of invoices.
/// </summary>
public static class InvoiceProcessorAgent
{
    public static XiansAgent Setup(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new()
        {
            Name = "Invoice Processor Agent",
            Author = "Xians",
            Description = "Invoice Processor Agent is a agent that processes the invoice processing workflow.",
            Version = "1.0.0",
            Category = "Invoice",
            IsTemplate = true,

        });

        agent.Workflows.DefineCustom<InvoiceProcessorWorkflow>(new WorkflowOptions { Activable = false });

        return agent;
    }
}

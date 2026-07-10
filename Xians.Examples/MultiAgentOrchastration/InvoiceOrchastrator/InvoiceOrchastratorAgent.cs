using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;

namespace Xians.Examples.MultiAgentOrchastration.InvoiceOrchastrator;

/// <summary>
/// Registers the Invoice Orchestrator Agent and sets up its workflows.
/// This agent receives invoices via webhook and orchestrates the overall processing flow.
/// </summary>
public static class InvoiceOrchastratorAgent
{
    public static XiansAgent Setup(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new()
        {
            Name = "Invoice Orchestrator Agent",
            Author = "Xians",
            Description = "Invoice Orchestrator Agent is a agent that orchestrates the invoice processing workflow.",
            Version = "1.0.0",
            Category = "Invoice",
            IsTemplate = true,
        });

        // Listens for incoming invoice webhooks
        var integratorWorkflow = agent.Workflows.DefineIntegrator();
        integratorWorkflow.OnWebhook((context) =>
        {
            Console.WriteLine($"InvoiceOrchastratorAgent received webhook: {context.Webhook.Name}");
            Console.WriteLine($"Payload: {context.Webhook.Payload}");

            var invoiceId = "dummy-invoice-id"; // get from context.Webhook.Payload;

            XiansContext.Workflows.StartAsync<InvoiceOrchastratorWorkflow>([invoiceId]);

            context.Respond(new { status = "received", webhook = context.Webhook.Name });
        });

        // Orchestration workflow, started by the webhook handler rather than activated directly
        agent.Workflows.DefineCustom<InvoiceOrchastratorWorkflow>(new WorkflowOptions { Activable = false });

        return agent;
    }
}

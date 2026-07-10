using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;

namespace Xians.Examples.MultiAgentOrchastration.InvoiceProcessor;

[Description("Processes an invoice through to completion")]
[Workflow("Invoice Processor Agent:Invoice Processing Workflow")]
public class InvoiceProcessorWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(
        [Description("The invoice ID to process")]
        string invoiceId
    )
    {
        Workflow.Logger.LogInformation("Processing started for invoice: {InvoiceId}", invoiceId);

        // Placeholder for real invoice processing logic
        await Task.CompletedTask;

        return $"Invoice {invoiceId} processed successfully";
    }
}

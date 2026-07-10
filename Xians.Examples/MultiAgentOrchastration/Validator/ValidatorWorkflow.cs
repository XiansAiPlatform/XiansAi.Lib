using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;

namespace Xians.Examples.MultiAgentOrchastration.Validator;

[Description("Validates the data and structure of an invoice")]
[Workflow("Validator Agent:Validation Workflow")]
public class ValidatorWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(
        [Description("The invoice ID to validate")]
        string invoiceId
    )
    {
        Workflow.Logger.LogInformation("Validation started for invoice: {InvoiceId}", invoiceId);

        // Placeholder for real validation logic
        await Task.CompletedTask;

        return $"Invoice {invoiceId} passed validation";
    }
}

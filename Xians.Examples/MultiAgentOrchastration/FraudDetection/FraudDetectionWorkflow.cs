using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;

namespace Xians.Examples.MultiAgentOrchastration.FraudDetection;

[Description("Analyzes an invoice for signs of fraud")]
[Workflow("Fraud Detection Agent:Fraud Detection Workflow")]
public class FraudDetectionWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(
        [Description("The invoice ID to analyze for fraud")]
        string invoiceId
    )
    {
        Workflow.Logger.LogInformation("Fraud detection started for invoice: {InvoiceId}", invoiceId);

        // Placeholder for real fraud detection logic
        await Task.CompletedTask;

        return $"Invoice {invoiceId} passed fraud detection checks";
    }
}

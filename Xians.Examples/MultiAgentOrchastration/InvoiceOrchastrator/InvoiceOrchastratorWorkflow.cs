using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Examples.MultiAgentOrchastration.FraudDetection;
using Xians.Examples.MultiAgentOrchastration.InvoiceProcessor;
using Xians.Examples.MultiAgentOrchastration.Validator;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows;

namespace Xians.Examples.MultiAgentOrchastration.InvoiceOrchastrator;

[Description("Orchestrates the end-to-end invoice processing flow")]
[Workflow("Invoice Orchestrator Agent:Invoice Orchestrator Workflow")]
public class InvoiceOrchastratorWorkflow
{

    private string fraudDetectionActivationName = "Fraud Detection Agent";
    private string validatorActivationName = "Validator Agent";
    private string processingActivationName = "Invoice Processor Agent";

    [WorkflowRun]
    public async Task<string> RunAsync(
        [Description("The invoice ID to orchestrate")]
        string invoiceId
    )
    {
        Workflow.Logger.LogInformation("Orchestration started for invoice: {InvoiceId}", invoiceId);

        // Run fraud detection and validation as parallel child workflows.
        // These belong to different agents, so each child is targeted at an activation
        // of its own agent via activationName. The invoice ID is used as the unique key
        // to keep child workflow IDs distinct across concurrent orchestrations.
        var fraudDetectionTask = ExecuteChildAsync<FraudDetectionWorkflow>(
            fraudDetectionActivationName, invoiceId);
        var validationTask = ExecuteChildAsync<ValidatorWorkflow>(
            validatorActivationName, invoiceId);

        // Workflow-safe alternative to Task.WhenAll (keeps the workflow deterministic)
        var results = await Workflow.WhenAllAsync(fraudDetectionTask, validationTask);

        Workflow.Logger.LogInformation("Fraud detection result: {Result}", results[0]);
        Workflow.Logger.LogInformation("Validation result: {Result}", results[1]);

        // Both checks passed - process the invoice
        var processingResult = await ExecuteChildAsync<InvoiceProcessorWorkflow>(
            processingActivationName, invoiceId);

        Workflow.Logger.LogInformation("Processing result: {Result}", processingResult);

        return $"Invoice {invoiceId} orchestration completed";
    }

    /// <summary>
    /// Executes a child workflow under a specific activation of its own agent, logging and
    /// rethrowing when the target activation is missing or deactivated.
    /// </summary>
    private static async Task<string?> ExecuteChildAsync<TWorkflow>(
        string activationName,
        string invoiceId)
    {
        try
        {
            return await XiansContext.Workflows
                .ExecuteAsync<TWorkflow, string>(
                    [invoiceId], uniqueKey: invoiceId, activationName: activationName);
        }
        catch (ActivationNotFoundException ex)
        {
            Workflow.Logger.LogWarning(
                "Activation '{ActivationName}' does not exist in the tenant: {Message}",
                activationName, ex.Message);
            return null;

        }
        catch (ActivationDeactivatedException ex)
        {
            Workflow.Logger.LogWarning(
                "activation '{ActivationName}' is deactivated in the tenant: {Message}",
                activationName, ex.Message);
            return null;

        }
    }
}

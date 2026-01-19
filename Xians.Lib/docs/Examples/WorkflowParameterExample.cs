using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace Xians.Lib.Examples;

/// <summary>
/// Example demonstrating how to use Description attribute to add descriptions
/// to workflow parameters and the workflow itself.
/// </summary>
[Description("Processes customer orders from submission to completion")]
[Workflow("ExampleAgent:OrderProcessing")]
public class OrderProcessingWorkflow
{
    [WorkflowRun]
    public async Task<OrderResult> RunAsync(
        [Description("The unique identifier for the order")]
        string orderId,
        
        [Description("The customer ID associated with this order")]
        string customerId,
        
        [Description("The total amount for the order in USD")]
        decimal amount)
    {
        Workflow.Logger.LogInformation(
            "Processing order {OrderId} for customer {CustomerId} with amount {Amount}",
            orderId, customerId, amount);

        // Workflow logic here...
        await Workflow.DelayAsync(TimeSpan.FromSeconds(1));

        return new OrderResult
        {
            OrderId = orderId,
            Status = "Completed",
            ProcessedAt = DateTime.UtcNow
        };
    }
}

public class OrderResult
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}

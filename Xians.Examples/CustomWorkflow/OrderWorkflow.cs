using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace Xians.Examples.CustomWorkflow;

/// <summary>
/// Example demonstrating how to use Description attribute to add descriptions
/// to workflow parameters and the workflow itself.
/// </summary>
[Description("Processes customer orders from submission to completion")]
[Workflow("Order Manager Agent:Order Workflow")]
public class OrderWorkflow
{
    [WorkflowRun]
    public async Task<OrderResult> RunAsync(
        [Description("The unique identifier for the order")]
        int orderId,
        
        [Description("The customer ID associated with this order")]
        string customerId,
        
        [Description("The total amount for the order in USD")]
        decimal amount,
        
        [Description("The status of the order")]
        string status = "Pending")
    {
        Workflow.Logger.LogInformation(
            "Processing order {OrderId} for customer {CustomerId} with amount {Amount}",
            orderId, customerId, amount);

        // Workflow logic here...
        await Workflow.DelayAsync(TimeSpan.FromSeconds(1));

        return new OrderResult
        {
            OrderId = orderId,
            Status = status,
            CustomerId = customerId,
            Amount = amount,
            ProcessedAt = DateTime.UtcNow
        };
    }
}

public class OrderResult
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ProcessedAt { get; set; }
}

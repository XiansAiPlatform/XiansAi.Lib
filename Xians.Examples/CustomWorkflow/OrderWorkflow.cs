using System.ComponentModel;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;

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
        [Description("The customer ID associated with this order")]
        string customerId,

        [Description("The total amount for the order in USD")]
        decimal amount
    )
    {
        var status = "Agent Approved";
        var comment = "Automatically approved by agent based on the amount";

        // call HITL task if amount is greater than 100
        if (amount > 100)
        {
        var taskHandle = await XiansContext.CurrentAgent.Tasks.StartTaskAsync(
            new TaskWorkflowRequest
            {
                Title = "Approve Order",
                Description = $"Review and approve order for customer {customerId} with amount ${amount}",
                DraftWork = $"Order Details:\nCustomer: {customerId}\nAmount: ${amount}",
                Actions = ["approve", "reject", "hold"],
                Timeout = TimeSpan.FromSeconds(20)
                }
            );

            // Wait for human decision
            var result = await XiansContext.CurrentAgent.Tasks.GetResultAsync(taskHandle);

            //result.

            comment = result.Comment;
            if (result.TimedOut)
            {
                status = "Timed Out";
            }
            else
            {
                status = result.PerformedAction switch
                {
                    "approve" => "Human Approved",
                    "reject" => "Human Rejected",
                    "hold" => "On Hold",
                    _ => "Unknown"
                };
            }
        }

        return new OrderResult
        {
            OrderId = Guid.NewGuid(),
            CustomerId = customerId,
            Amount = amount,
            ProcessedAt = DateTime.UtcNow,
            Status = status,
            Comment = comment
        };
    }
}

public class OrderResult
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "Pending";
    public required string CustomerId { get; set;}
    public decimal Amount { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? Comment { get; set; }
}

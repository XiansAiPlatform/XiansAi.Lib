using Temporalio.Workflows;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Examples;

/// <summary>
/// Example demonstrating sub-workflow usage in Xians.Lib.
/// This example shows how to orchestrate multiple workflows as part of a larger business process.
/// 
/// ⚠️ IMPORTANT: Workflow Determinism
/// When writing workflow code, you MUST use Temporal's workflow-safe methods:
/// 
/// ❌ DO NOT USE:
/// - Task.Run, Task.Delay, Task.Wait
/// - Task.WhenAny, Task.WhenAll (use Workflow.WhenAnyAsync/WhenAllAsync)
/// - Task.ConfigureAwait(false)
/// - DateTime.UtcNow (use Workflow.UtcNow)
/// - Thread pool operations
/// 
/// ✅ DO USE:
/// - Workflow.RunTaskAsync (instead of Task.Run)
/// - Workflow.DelayAsync (instead of Task.Delay)
/// - Workflow.WhenAnyAsync/WhenAllAsync (instead of Task.WhenAny/All)
/// - Workflow.WaitConditionAsync (for conditional waits)
/// - Workflow.UtcNow (for current time)
/// 
/// This ensures workflow determinism and proper replay behavior.
/// </summary>

// =====================================================
// EXAMPLE 1: Simple Parent-Child Workflow
// =====================================================

public class OrderData
{
    public string OrderId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public decimal Amount { get; set; }
}

public class PaymentResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = "";
}

/// <summary>
/// Parent workflow that orchestrates order processing.
/// Demonstrates starting and executing child workflows.
/// </summary>
[Workflow("OrderService:ProcessOrder")]
public class ProcessOrderWorkflow
{
    [WorkflowRun]
    public async Task<bool> RunAsync(OrderData order)
    {
        // Execute payment workflow and wait for result
        var paymentResult = await XiansContext.ExecuteWorkflowAsync<PaymentResult>(
            "PaymentService:ProcessPayment",
            order.OrderId,  // Use order ID as workflow postfix for uniqueness
            order.CustomerId,
            order.Amount
        );

        if (!paymentResult.Success)
        {
            // Payment failed - could trigger compensation logic here
            return false;
        }

        // Start fulfillment workflow (fire and forget)
        await XiansContext.StartWorkflowAsync(
            "FulfillmentService:StartFulfillment",
            order.OrderId,
            order
        );

        // Start notification workflow in parallel (fire and forget)
        await XiansContext.StartWorkflowAsync(
            "NotificationService:SendOrderConfirmation",
            order.OrderId,
            order.CustomerId,
            paymentResult.TransactionId
        );

        return true;
    }
}

/// <summary>
/// Child workflow that processes payment.
/// Can be executed independently or as a child workflow.
/// </summary>
[Workflow("PaymentService:ProcessPayment")]
public class ProcessPaymentWorkflow
{
    [WorkflowRun]
    public async Task<PaymentResult> RunAsync(string customerId, decimal amount)
    {
        // ✅ Use Workflow.DelayAsync for deterministic delays (not Task.Delay)
        await Workflow.DelayAsync(TimeSpan.FromSeconds(2));

        return new PaymentResult
        {
            Success = true,
            TransactionId = $"TXN-{Guid.NewGuid()}"
        };
    }
}

// =====================================================
// EXAMPLE 2: Parallel Sub-Workflow Execution
// =====================================================

public class NotificationData
{
    public string UserId { get; set; } = "";
    public string Message { get; set; } = "";
    public string Channel { get; set; } = ""; // email, sms, push
}

/// <summary>
/// Demonstrates parallel execution of multiple sub-workflows.
/// Useful for fan-out scenarios like bulk notifications.
/// </summary>
[Workflow("NotificationService:BulkNotification")]
public class BulkNotificationWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(List<string> userIds, string message)
    {
        // Start a sub-workflow for each user in parallel
        var notificationTasks = userIds.Select(userId =>
            XiansContext.StartWorkflowAsync(
                "NotificationService:SendNotification",
                userId,  // Use userId as postfix for unique workflow IDs
                new NotificationData
                {
                    UserId = userId,
                    Message = message,
                    Channel = "email"
                }
            )
        );

        // Wait for all sub-workflows to start
        // ✅ Use Workflow.WhenAllAsync for deterministic execution
        await Workflow.WhenAllAsync(notificationTasks);

        // All notifications have been initiated
        // The actual sending happens asynchronously in the child workflows
    }
}

[Workflow("NotificationService:SendNotification")]
public class SendNotificationWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(NotificationData notification)
    {
        // ✅ Use Workflow.DelayAsync instead of Task.Delay for deterministic delays
        await Workflow.DelayAsync(TimeSpan.FromSeconds(1));
        
        // In real implementation, this would call an activity to send the notification
        // Activity call example:
        // await Workflow.ExecuteActivityAsync(
        //     () => notificationActivity.SendAsync(notification),
        //     new() { StartToCloseTimeout = TimeSpan.FromMinutes(1) }
        // );
    }
}

// =====================================================
// EXAMPLE 3: Using Type-Safe Workflow References
// =====================================================

/// <summary>
/// Demonstrates using generic type parameters for type-safe sub-workflow execution.
/// </summary>
[Workflow("DataService:ProcessData")]
public class DataProcessingWorkflow
{
    [WorkflowRun]
    public async Task<ProcessingResult> RunAsync(string dataId)
    {
        // Execute sub-workflow using generic type parameter
        // This provides compile-time type safety
        var validationResult = await XiansContext.ExecuteWorkflowAsync<
            ValidateDataWorkflow,
            ValidationResult
        >(
            dataId,  // ID postfix
            dataId   // Argument
        );

        if (!validationResult.IsValid)
        {
            return new ProcessingResult { Success = false };
        }

        // Start transformation workflow
        await XiansContext.StartWorkflowAsync<TransformDataWorkflow>(
            dataId,
            dataId,
            validationResult.Schema
        );

        return new ProcessingResult { Success = true };
    }
}

[Workflow("DataService:ValidateData")]
public class ValidateDataWorkflow
{
    [WorkflowRun]
    public async Task<ValidationResult> RunAsync(string dataId)
    {
        // ✅ Use Workflow.DelayAsync for deterministic delays
        await Workflow.DelayAsync(TimeSpan.FromSeconds(1));
        return new ValidationResult { IsValid = true, Schema = "v1" };
    }
}

[Workflow("DataService:TransformData")]
public class TransformDataWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(string dataId, string schema)
    {
        // ✅ Use Workflow.DelayAsync for deterministic delays
        await Workflow.DelayAsync(TimeSpan.FromSeconds(2));
        // Transform data using schema
    }
}

// Supporting classes
public class ProcessingResult
{
    public bool Success { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Schema { get; set; } = "";
}

// =====================================================
// EXAMPLE 4: Using Outside Workflow Context
// =====================================================

/// <summary>
/// Demonstrates calling sub-workflow APIs from outside a workflow context.
/// Useful for API controllers, background services, etc.
/// </summary>
public class OrderService
{
    /// <summary>
    /// API endpoint handler that starts a workflow from a non-workflow context.
    /// </summary>
    public async Task<string> CreateOrderAsync(OrderData order)
    {
        // When called outside a workflow, this will use the Temporal client
        // to start a new independent workflow (not a child workflow)
        await XiansContext.StartWorkflowAsync(
            "OrderService:ProcessOrder",
            order.OrderId,
            order
        );

        return $"Order {order.OrderId} processing started";
    }

    /// <summary>
    /// API endpoint handler that executes a workflow and waits for result.
    /// </summary>
    public async Task<PaymentResult> ProcessPaymentAsync(string customerId, decimal amount)
    {
        // When called outside a workflow, this will execute via Temporal client
        // and wait for the workflow to complete
        var result = await XiansContext.ExecuteWorkflowAsync<PaymentResult>(
            "PaymentService:ProcessPayment",
            $"payment-{Guid.NewGuid()}", // Unique postfix
            customerId,
            amount
        );

        return result;
    }
}

// =====================================================
// EXAMPLE 5: Advanced Error Handling
// =====================================================

/// <summary>
/// Demonstrates error handling and compensation patterns with sub-workflows.
/// </summary>
[Workflow("OrderService:OrderWithCompensation")]
public class OrderWithCompensationWorkflow
{
    [WorkflowRun]
    public async Task<bool> RunAsync(OrderData order)
    {
        PaymentResult? paymentResult = null;
        bool inventoryReserved = false;

        try
        {
            // Step 1: Reserve inventory
            inventoryReserved = await XiansContext.ExecuteWorkflowAsync<bool>(
                "InventoryService:ReserveInventory",
                order.OrderId,
                order
            );

            if (!inventoryReserved)
            {
                return false;
            }

            // Step 2: Process payment
            paymentResult = await XiansContext.ExecuteWorkflowAsync<PaymentResult>(
                "PaymentService:ProcessPayment",
                order.OrderId,
                order.CustomerId,
                order.Amount
            );

            if (!paymentResult.Success)
            {
                // Payment failed - compensate by releasing inventory
                await XiansContext.StartWorkflowAsync(
                    "InventoryService:ReleaseInventory",
                    order.OrderId,
                    order
                );
                return false;
            }

            // Step 3: Create shipment
            await XiansContext.StartWorkflowAsync(
                "ShippingService:CreateShipment",
                order.OrderId,
                order
            );

            return true;
        }
        catch (Exception)
        {
            // Handle errors and compensate
            if (inventoryReserved)
            {
                await XiansContext.StartWorkflowAsync(
                    "InventoryService:ReleaseInventory",
                    order.OrderId,
                    order
                );
            }

            if (paymentResult?.Success == true)
            {
                await XiansContext.StartWorkflowAsync(
                    "PaymentService:RefundPayment",
                    order.OrderId,
                    paymentResult.TransactionId
                );
            }

            throw; // Re-throw to mark workflow as failed
        }
    }
}


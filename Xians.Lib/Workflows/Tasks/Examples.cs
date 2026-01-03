using Temporalio.Workflows;
using Xians.Lib.Agents.Tasks;
using Xians.Lib.Agents.Tasks.Models;

namespace Xians.Lib.Workflows.Tasks.Examples;

/// <summary>
/// Example workflows demonstrating how to use the Task Workflow SDK.
/// </summary>
public static class TaskWorkflowExamples
{
    /// <summary>
    /// Example 1: Simple approval workflow that creates a task and waits for completion.
    /// </summary>
    [Workflow("Examples:Simple Approval")]
    public class SimpleApprovalWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string documentId, string approverId)
        {
            // Create a task and wait for the approver to complete it
            var result = await TaskWorkflowService.CreateAndWaitAsync(
                taskId: $"approve-{documentId}",
                title: "Approve Document",
                description: $"Please review and approve document {documentId}",
                participantId: approverId
            );

            if (result.Success)
            {
                return $"Document approved at {result.CompletedAt}";
            }
            else
            {
                return $"Approval failed: {result.RejectionReason}";
            }
        }
    }

    /// <summary>
    /// Example 2: Multi-stage workflow with multiple tasks in sequence.
    /// </summary>
    [Workflow("Examples:Multi Stage Approval")]
    public class MultiStageApprovalWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string requestId, string managerId, string directorId)
        {
            // Stage 1: Manager approval
            var managerResult = await TaskWorkflowService.CreateAndWaitAsync(
                taskId: $"{requestId}-manager",
                title: "Manager Approval Required",
                description: "Please review this budget request",
                participantId: directorId,
                draftWork: "Initial budget proposal"
            );

            if (!managerResult.Success)
            {
                return $"Manager rejected: {managerResult.RejectionReason}";
            }

            // Stage 2: Director approval (with manager's feedback)
            var directorResult = await TaskWorkflowService.CreateAndWaitAsync(
                taskId: $"{requestId}-director",
                title: "Director Approval Required",
                description: "Final approval needed for budget request",
                participantId: "cfo@example.com",
                draftWork: managerResult.FinalWork
            );

            if (!directorResult.Success)
            {
                return $"Director rejected: {directorResult.RejectionReason}";
            }

            return $"Fully approved! Final version: {directorResult.FinalWork}";
        }
    }

    /// <summary>
    /// Example 3: Parallel task execution with multiple reviewers.
    /// Uses CreateAndWaitAsync in parallel for proper result handling.
    /// </summary>
    [Workflow("Examples:Parallel Review")]
    public class ParallelReviewWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string documentId, string[] reviewers)
        {
            // Create tasks for all reviewers in parallel and wait for completion
            var tasks = reviewers.Select(reviewer =>
                TaskWorkflowService.CreateAndWaitAsync(
                    taskId: $"review-{documentId}-{reviewer}",
                    title: $"Review Document {documentId}",
                    description: "Please provide your feedback",
                    participantId: "coordinator@example.com",
                    metadata: new Dictionary<string, object>
                    {
                        { "documentId", documentId },
                        { "reviewer", reviewer }
                    }
                )
            ).ToList();

            // Wait for all reviews to complete
            var results = await Task.WhenAll(tasks);

            // Count successful reviews
            var successCount = results.Count(r => r.Success);
            var failCount = results.Length - successCount;

            return $"Reviews completed: {successCount} successful, {failCount} failed";
        }
    }

    /// <summary>
    /// Example 4: Task with rich metadata and monitoring.
    /// </summary>
    [Workflow("Examples:Task With Metadata")]
    public class TaskWithMetadataWorkflow
    {
        [WorkflowRun]
        public async Task<TaskWorkflowResult> RunAsync(string orderId, decimal amount)
        {
            var metadata = new Dictionary<string, object>
            {
                { "orderId", orderId },
                { "amount", amount },
                { "currency", "USD" },
                { "department", "Sales" },
                { "priority", "High" },
                { "createdAt", DateTime.UtcNow }
            };

            var request = new TaskWorkflowRequest
            {
                TaskId = $"process-order-{orderId}",
                Title = $"Process Order #{orderId}",
                Description = $"Review and process order for ${amount}",
                ParticipantId = "sales-manager@example.com",
                DraftWork = "Order details to be reviewed...",
                Metadata = metadata
            };

            return await TaskWorkflowService.CreateAndWaitAsync(
                request
            );
        }
    }

    /// <summary>
    /// Example 5: Conditional task creation based on business logic.
    /// </summary>
    [Workflow("Examples:Conditional Task")]
    public class ConditionalTaskWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(decimal purchaseAmount, string requesterId)
        {
            const decimal approvalThreshold = 10000;

            if (purchaseAmount < approvalThreshold)
            {
                // Auto-approve small purchases
                return $"Auto-approved: Amount ${purchaseAmount} is below threshold";
            }

            // Require approval for large purchases
            var result = await TaskWorkflowService.CreateAndWaitAsync(
                taskId: $"large-purchase-{Guid.NewGuid()}",
                title: "Large Purchase Approval",
                description: $"Approval needed for ${purchaseAmount} purchase by {requesterId}",
                participantId: "finance-manager@example.com",
                metadata: new Dictionary<string, object>
                {
                    { "amount", purchaseAmount },
                    { "requesterId", requesterId },
                    { "threshold", approvalThreshold }
                }
            );

            if (result.Success)
            {
                return $"Purchase approved: {result.FinalWork}";
            }
            else
            {
                return $"Purchase rejected: {result.RejectionReason}";
            }
        }
    }

    /// <summary>
    /// Example 6: Signaling an existing task from within a workflow.
    /// </summary>
    [Workflow("Examples:Task Interaction")]
    public class TaskInteractionWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string existingTaskId, string newDraftContent)
        {
            // Update the draft of an existing task
            await TaskWorkflowService.UpdateDraftAsync(existingTaskId, newDraftContent);

            // Wait for some condition or time
            await Workflow.DelayAsync(TimeSpan.FromSeconds(10));

            // You could complete the task programmatically
            await TaskWorkflowService.CompleteTaskAsync(existingTaskId);
            
            return $"Task {existingTaskId} updated and completed";
        }
    }
}


using System.ComponentModel;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;

namespace Xians.Examples.CustomWorkflow;

/// <summary>
/// AI-driven debt collection workflow with human-in-the-loop controls
/// for all customer communications and sensitive decisions.
/// </summary>
[Description("Manages the full debt collection lifecycle with AI orchestration and human oversight")]
[Workflow("Case Manager Agent:Debt Collection Workflow")]
public class CaseWorkflow
{
    [WorkflowRun]
    public async Task<CaseResult> RunAsync(
        [Description("Unique identifier for the debt collection case")]
        string caseId
    )
    {
        // Simulate fetching case data from database/external system
        var caseData = await FetchCaseDataAsync(caseId);

        var caseResult = new CaseResult
        {
            CaseId = caseId,
            CustomerId = caseData.CustomerId,
            OutstandingAmount = caseData.OutstandingAmount,
            StartedAt = DateTime.UtcNow,
            Status = "In Progress",
            ActivityLog = new List<string>()
        };

        // Step 1: AI Case Assessment
        caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Case assessment completed. Risk Level: {caseData.RiskLevel}");

        // Escalate immediately if high risk
        if (caseData.RiskLevel.Equals("High", StringComparison.OrdinalIgnoreCase))
        {
            caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] High risk detected - escalating to human case handler");
            caseResult.Status = "Escalated to Human";
            caseResult.CompletedAt = DateTime.UtcNow;
            return caseResult;
        }

        // Step 2: First Email Communication (HITL)
        var initialEmailResult = await RequestHumanApprovalForEmailAsync(
            caseId,
            "initial-email",
            "Initial Contact Email",
            $"Dear Customer,\n\nWe noticed an outstanding balance of ${caseData.OutstandingAmount} on your account.\n" +
            $"We would like to work with you to resolve this matter.\n\nPlease contact us to discuss payment options.",
            caseData.CustomerId,
            caseData.OutstandingAmount
        );

        if (initialEmailResult.Action == "reject" || initialEmailResult.Action == "escalate")
        {
            caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Initial email rejected - escalating to human");
            caseResult.Status = "Escalated to Human";
            caseResult.CompletedAt = DateTime.UtcNow;
            return caseResult;
        }

        caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Initial email sent: {initialEmailResult.Comment}");

        // Simulate waiting for customer response
        await Workflow.DelayAsync(TimeSpan.FromSeconds(5));

        // Simulate customer response
        var customerResponse = "I received your email about the outstanding balance. " +
                              "I'm currently facing some financial difficulties and would like to discuss payment options. " +
                              "Can we set up a payment plan?";

        // Step 3: Responding to Customer Reply (HITL)
        var customerReplyResponse = await RequestHumanApprovalForCustomerReplyAsync(
            caseId,
            caseData.CustomerId,
            caseData.OutstandingAmount,
            customerResponse
        );

        if (customerReplyResponse.Action == "reject" || customerReplyResponse.Action == "escalate")
        {
            caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Reply response rejected - escalating to human");
            caseResult.Status = "Escalated to Human";
            caseResult.CompletedAt = DateTime.UtcNow;
            return caseResult;
        }

        caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Customer reply sent: {customerReplyResponse.Comment}");

        // Step 4: Propose Payment Plan (HITL)
        var paymentPlanResult = await RequestPaymentPlanApprovalAsync(
            caseId,
            caseData.CustomerId,
            caseData.OutstandingAmount
        );

        if (paymentPlanResult.Action == "reject" || paymentPlanResult.Action == "escalate")
        {
            caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Payment plan rejected - escalating to human");
            caseResult.Status = "Escalated to Human";
            caseResult.CompletedAt = DateTime.UtcNow;
            return caseResult;
        }

        caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Payment plan proposed and approved: {paymentPlanResult.Comment}");
        caseResult.PaymentPlanAccepted = paymentPlanResult.Action == "approve";

        // Simulate payment monitoring period
        await Workflow.DelayAsync(TimeSpan.FromSeconds(3));

        // Step 5: Missed Payment Alert (HITL)
        var missedPaymentResult = await RequestMissedPaymentActionAsync(
            caseId,
            caseData.CustomerId,
            caseData.OutstandingAmount
        );

        caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Missed payment handled: {missedPaymentResult.Comment}");

        // Step 6: Escalation Recommendation (HITL)
        if (missedPaymentResult.Action == "send-reminder")
        {
            var escalationResult = await RequestEscalationDecisionAsync(
                caseId,
                caseData.CustomerId,
                caseData.OutstandingAmount,
                caseResult.ActivityLog.Count
            );

            if (escalationResult.Action == "escalate")
            {
                caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Case escalated to human case handler: {escalationResult.Comment}");
                caseResult.Status = "Escalated to Human";
            }
            else
            {
                caseResult.ActivityLog.Add($"[{DateTime.UtcNow}] Case continues with AI handling: {escalationResult.Comment}");
                caseResult.Status = "Active - AI Managed";
            }
        }
        else
        {
            caseResult.Status = "Resolved";
        }

        caseResult.CompletedAt = DateTime.UtcNow;
        return caseResult;
    }

    private async Task<CaseData> FetchCaseDataAsync(string caseId)
    {
        // Simulate fetching case data from external system or database
        // In a real implementation, this would call an activity to fetch data
        await Workflow.DelayAsync(TimeSpan.FromMilliseconds(100));

        // Return dummy data for demonstration
        return new CaseData
        {
            CaseId = caseId,
            CustomerId = $"CUST-{caseId.Substring(0, Math.Min(5, caseId.Length))}",
            OutstandingAmount = 1250.00m,
            PaymentHistory = "2 missed payments in last 6 months",
            RiskLevel = "Medium"
        };
    }

    private async Task<HumanDecision> RequestHumanApprovalForEmailAsync(
        string caseId,
        string emailType,
        string title,
        string draftContent,
        string customerId,
        decimal amount)
    {
        var taskHandle = await XiansContext.CurrentAgent.Tasks.StartTaskAsync(
            new TaskWorkflowRequest
            {
                TaskId = $"{caseId}-{emailType}-{Workflow.NewGuid()}",
                Title = title,
                Description = $"Review and approve AI-generated email for case {caseId} (Customer: {customerId}, Amount: ${amount})",
                DraftWork = $"--- Draft Email ---\n{draftContent}\n\n--- Customer Details ---\nCustomer ID: {customerId}\nOutstanding Amount: ${amount}",
                Actions = ["approve", "reject", "escalate"],
                //Timepout = TimeSpan.FromSeconds(10)
            }
        );

        var result = await XiansContext.CurrentAgent.Tasks.GetResultAsync(taskHandle);

        //result.Complete();

        var finalWork = result.FinalWork;

        return new HumanDecision
        {
            Action = result.PerformedAction ?? "reject",
            Comment = result.Comment ?? string.Empty
        };
    }

    private async Task<HumanDecision> RequestHumanApprovalForCustomerReplyAsync(
        string caseId,
        string customerId,
        decimal amount,
        string customerMessage)
    {
        var draftReply = $"Dear Customer,\n\n" +
                        $"Thank you for your response. We understand your situation and appreciate you reaching out to us.\n\n" +
                        $"We would be happy to work with you on a payment plan. We can offer you flexible payment options " +
                        $"to help resolve the outstanding amount of ${amount}.\n\n" +
                        $"Please let us know what works best for you, and we'll arrange a suitable plan.\n\n" +
                        $"Best regards,\nDebt Collection Team";

        var taskHandle = await XiansContext.CurrentAgent.Tasks.StartTaskAsync(
            new TaskWorkflowRequest
            {
                TaskId = $"{caseId}-customer-reply-{Workflow.NewGuid()}",
                Title = "Response to Customer Inquiry",
                Description = $"Review AI response to customer inquiry for case {caseId} (Customer: {customerId}, Amount: ${amount})",
                DraftWork = $"--- Customer's Message ---\n{customerMessage}\n\n" +
                           $"--- AI-Generated Reply ---\n{draftReply}\n\n" +
                           $"--- Customer Details ---\nCustomer ID: {customerId}\nOutstanding Amount: ${amount}",
                Actions = ["approve", "edit-and-approve", "reject", "escalate"]
            }
        );

        var result = await XiansContext.CurrentAgent.Tasks.GetResultAsync(taskHandle);

        return new HumanDecision
        {
            Action = result.PerformedAction ?? "reject",
            Comment = result.Comment ?? string.Empty
        };
    }

    private async Task<HumanDecision> RequestPaymentPlanApprovalAsync(
        string caseId,
        string customerId,
        decimal amount)
    {
        var monthlyInstallment = Math.Round(amount / 6, 2);
        var draftPlan = $"Payment Plan Proposal:\n" +
                       $"- Total Amount: ${amount}\n" +
                       $"- Duration: 6 months\n" +
                       $"- Monthly Payment: ${monthlyInstallment}\n" +
                       $"- First Payment Due: {DateTime.UtcNow.AddDays(7):yyyy-MM-dd}";

        var taskHandle = await XiansContext.CurrentAgent.Tasks.StartTaskAsync(
            new TaskWorkflowRequest
            {
                TaskId = $"{caseId}-payment-plan-{Workflow.NewGuid()}",
                Title = "Approve Payment Plan Proposal",
                Description = $"Review AI-proposed payment plan for customer {customerId}",
                DraftWork = draftPlan,
                Actions = ["approve", "modify", "reject", "escalate"]
            }
        );

        var result = await XiansContext.CurrentAgent.Tasks.GetResultAsync(taskHandle);

        return new HumanDecision
        {
            Action = result.PerformedAction ?? "reject",
            Comment = result.Comment ?? string.Empty
        };
    }

    private async Task<HumanDecision> RequestMissedPaymentActionAsync(
        string caseId,
        string customerId,
        decimal amount)
    {
        var draftAlert = $"Missed Payment Detected:\n" +
                        $"Customer: {customerId}\n" +
                        $"Outstanding: ${amount}\n" +
                        $"Recommended Action: Send automated reminder with offer to adjust payment plan";

        var taskHandle = await XiansContext.CurrentAgent.Tasks.StartTaskAsync(
            new TaskWorkflowRequest
            {
                TaskId = $"{caseId}-missed-payment-{Workflow.NewGuid()}",
                Title = "Handle Missed Payment",
                Description = $"Customer {customerId} missed a scheduled payment",
                DraftWork = draftAlert,
                Actions = ["send-reminder", "adjust-plan", "escalate", "waive"]
            }
        );

        var result = await XiansContext.CurrentAgent.Tasks.GetResultAsync(taskHandle);

        return new HumanDecision
        {
            Action = result.PerformedAction ?? "escalate",
            Comment = result.Comment ?? string.Empty
        };
    }

    private async Task<HumanDecision> RequestEscalationDecisionAsync(
        string caseId,
        string customerId,
        decimal amount,
        int interactionCount)
    {
        var recommendation = $"Escalation Recommendation:\n" +
                           $"Case ID: {caseId}\n" +
                           $"Customer: {customerId}\n" +
                           $"Outstanding: ${amount}\n" +
                           $"Interactions: {interactionCount}\n" +
                           $"AI Assessment: Multiple missed payments and limited engagement. " +
                           $"Recommend escalation to human case handler for personalized intervention.";

        var taskHandle = await XiansContext.CurrentAgent.Tasks.StartTaskAsync(
            new TaskWorkflowRequest
            {
                TaskId = $"{caseId}-escalation-decision-{Workflow.NewGuid()}",
                Title = "Escalation Decision Required",
                Description = $"AI recommends escalating case {caseId} to human case handler",
                DraftWork = recommendation,
                Actions = ["escalate", "continue-ai", "close-case", "legal-action"]
            }
        );

        var result = await XiansContext.CurrentAgent.Tasks.GetResultAsync(taskHandle);

        return new HumanDecision
        {
            Action = result.PerformedAction ?? "escalate",
            Comment = result.Comment ?? string.Empty
        };
    }
}

public class CaseResult
{
    public required string CaseId { get; set; }
    public required string CustomerId { get; set; }
    public decimal OutstandingAmount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public bool PaymentPlanAccepted { get; set; }
    public List<string> ActivityLog { get; set; } = new();
}

public class HumanDecision
{
    public required string Action { get; set; }
    public required string Comment { get; set; }
}

public class CaseData
{
    public required string CaseId { get; set; }
    public required string CustomerId { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string PaymentHistory { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "Medium";
}

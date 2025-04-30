using Temporalio.Workflows;

public class AgentContext {

    public static AgentContext? Instance { 
        get {
            var memoUtil = new MemoUtil(Workflow.Memo);
            if (Workflow.InWorkflow) {
                return new AgentContext {
                    Agent = memoUtil.GetAgent(),
                    WorkflowId = Workflow.Info.WorkflowId,
                    WorkflowType = Workflow.Info.WorkflowType,
                    TenantId = memoUtil.GetTenantId(),
                    QueueName = memoUtil.GetQueueName(),
                    AssignmentId = memoUtil.GetAssignment(),
                    UserId = memoUtil.GetUserId()
                };
            }
            return null;
        }
    }

    public required string Agent { get; set; }
    public string? QueueName { get; set; }
    public string? AssignmentId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string TenantId { get; set; }
    public required string UserId { get; set; }
}
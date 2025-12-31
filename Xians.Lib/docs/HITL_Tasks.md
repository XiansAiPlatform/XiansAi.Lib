# Human-in-the-Loop (HITL) Tasks

> **TL;DR**: Pause your agent workflows and wait for human input, decisions, or approvals. Your workflow continues exactly where it left off when the human completes their task.

## Why HITL?

AI agents are powerful, but sometimes you need a human to:
- **Approve** critical decisions (budget requests, content publication)
- **Refine** AI-generated work (edit drafts, review code)
- **Validate** results before proceeding (data quality checks)
- **Handle** edge cases AI can't resolve

Instead of polling databases or building complex callback systems, HITL tasks let your workflow simply **wait** for human action.

## Quick Start

### 1. Create and Wait

```csharp
[Workflow("Content Publishing Workflow")]
public class ContentPublishingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string contentUrl)
    {
        // AI generates the content
        var draft = await GenerateContentAsync(contentUrl);
        
        // Human reviews and approves
        var result = await TaskWorkflowService.CreateAndWaitAsync(
            taskId: $"approve-{Workflow.NewGuid()}",  // ⚠️ Use Workflow.NewGuid(), not Guid.NewGuid()
            title: "Approve Content",
            description: "Review AI-generated content before publishing",
            participantId: "editor@company.com",
            draftWork: draft
        );
        
        if (result.Success)
        {
            await PublishAsync(result.FinalWork);
            return "✅ Published!";
        }
        
        return $"❌ Rejected: {result.RejectionReason}";
    }
}
```

### 2. Human Completes the Task

From your UI/API, signal the task workflow:

```csharp
var client = await temporalService.GetClientAsync();
var workflowId = $"{tenantId}:Platform:Task Workflow:approve-{taskId}";
var handle = client.GetWorkflowHandle(workflowId);

// Option 1: Approve as-is
await handle.SignalAsync(wf => wf.CompleteTask());

// Option 2: Edit and approve
await handle.SignalAsync(wf => wf.UpdateDraft("Edited content..."));
await handle.SignalAsync(wf => wf.CompleteTask());

// Option 3: Reject
await handle.SignalAsync(wf => wf.RejectTask("Needs more research"));
```

## Common Patterns

### Multi-Stage Approval Pipeline

```csharp
// Manager reviews first
var managerReview = await TaskWorkflowService.CreateAndWaitAsync(
    taskId: $"budget-manager-{requestId}",
    title: "Manager: Review Budget Request",
    participantId: "manager@company.com",
    draftWork: initialProposal
);

if (!managerReview.Success) 
    return "Manager rejected";

// Director gets manager's version
var directorReview = await TaskWorkflowService.CreateAndWaitAsync(
    taskId: $"budget-director-{requestId}",
    title: "Director: Final Approval",
    participantId: "director@company.com",
    draftWork: managerReview.FinalWork  // Manager's edits
);

return directorReview.Success ? "Approved!" : "Rejected";
```

### Parallel Reviews

```csharp
var reviewers = new[] { "alice@co.com", "bob@co.com", "charlie@co.com" };

// Launch all reviews in parallel
var tasks = reviewers.Select(reviewer =>
    TaskWorkflowService.CreateAndWaitAsync(
        taskId: $"review-{documentId}-{reviewer}",
        title: "Code Review Required",
        participantId: reviewer
    )
).ToArray();

// Wait for all (use Workflow.WhenAllAsync)
var results = await Workflow.WhenAllAsync(tasks);

// Require majority approval
var approved = results.Count(r => r.Success);
return approved >= reviewers.Length / 2;
```

### Conditional HITL

```csharp
// Auto-approve small amounts, require human for large ones
if (amount < 1000)
    return "Auto-approved";

var result = await TaskWorkflowService.CreateAndWaitAsync(
    taskId: $"large-purchase-{Workflow.NewGuid()}",
    title: $"Approve ${amount} Purchase",
    participantId: "finance@company.com",
    metadata: new Dictionary<string, object>
    {
        { "amount", amount },
        { "autoApprovalThreshold", 1000 }
    }
);
```

### Fire-and-Forget Tasks

Sometimes you don't need to wait:

```csharp
// Create task but don't block the workflow
await TaskWorkflowService.CreateAsync(
    taskId: "notify-team-123",
    title: "FYI: Deployment Completed",
    description: "Review deployment logs if needed",
    participantId: "devops@company.com"
);

// Workflow continues immediately
await DoNextThingAsync();
```

## Key Concepts

### ⚠️ Temporal Workflow Constraints

Workflows must be **deterministic** for replay. Always use Temporal's workflow-specific methods:

#### Task Scheduling
```csharp
// ❌ DON'T - Uses default scheduler (non-deterministic)
await Task.Run(() => DoWork());

// ✅ DO - Uses workflow scheduler (deterministic)
await Workflow.RunTaskAsync(() => DoWork());

// ✅ ALSO OK - Explicit current scheduler
await Task.Factory.StartNew(() => DoWork(), TaskScheduler.Current);
```

#### Delays and Waiting
```csharp
// ❌ DON'T - Uses .NET timers (non-deterministic)
await Task.Delay(TimeSpan.FromMinutes(5));
Task.Wait();

// ✅ DO - Uses workflow timers (deterministic)
await Workflow.DelayAsync(TimeSpan.FromMinutes(5));
await Workflow.WaitConditionAsync(() => isReady);
```

#### Task Composition
```csharp
// ❌ DON'T - May be non-deterministic
var winner = await Task.WhenAny(task1, task2, task3);
var results = await Task.WhenAll(tasks);

// ✅ DO - Workflow-safe versions
var winner = await Workflow.WhenAnyAsync(task1, task2, task3);
var results = await Workflow.WhenAllAsync(tasks);
```

#### ConfigureAwait
```csharp
// ❌ DON'T - Loses workflow context
await SomeTask().ConfigureAwait(false);

// ✅ DO - Maintains workflow context (or omit entirely)
await SomeTask().ConfigureAwait(true);
await SomeTask();  // Default is true in workflows
```

> **Why?** Temporal replays workflows from history. Using .NET's default schedulers/timers produces different results during replay, causing non-determinism errors.

### Workflow IDs are Deterministic

```csharp
// ❌ BAD - Non-deterministic (fails on replay)
TaskId = $"task-{Guid.NewGuid()}"

// ✅ GOOD - Deterministic
TaskId = $"task-{Workflow.NewGuid()}"

// ✅ ALSO GOOD - Based on input
TaskId = $"approve-{documentId}"
```

**Why?** Temporal replays workflows from history. `Guid.NewGuid()` generates a different ID on replay, causing the dreaded `TMPRL1100` non-determinism error.

### Tasks are Child Workflows

- Tasks run as separate Temporal workflows
- They **survive** parent workflow termination (by default)
- They're searchable via Temporal UI/CLI
- They have their own event history

### Search & Filter

Tasks automatically get indexed search attributes:

```bash
# Find all pending tasks assigned to me
temporal workflow list \
  --query "participantId='me@company.com' AND WorkflowType='Platform:Task Workflow'"

# Find all tasks for a tenant
temporal workflow list \
  --query "tenantId='acme-corp'"
```

## Best Practices

### ✅ DO

- **Use meaningful task IDs**: `approve-doc-123` not `task-xyz`
- **Add rich metadata**: Helps UIs display context
- **Handle rejections**: Check `result.Success` and act accordingly
- **Use deterministic IDs**: `Workflow.NewGuid()` or input-based IDs
- **Use Workflow APIs**: `Workflow.DelayAsync`, `Workflow.WhenAllAsync`, etc.
- **Use Workflow.RunTaskAsync**: Never use `Task.Run` in workflows

### ❌ DON'T

- **Use `Guid.NewGuid()`**: Causes non-determinism errors
- **Use `Task.Run`**: Use `Workflow.RunTaskAsync` instead
- **Use `Task.Delay`**: Use `Workflow.DelayAsync` instead
- **Use `Task.WhenAny/All`**: Use `Workflow.WhenAnyAsync/AllAsync` instead
- **Use `ConfigureAwait(false)`**: Loses workflow context
- **Forget to wait**: Use `CreateAndWaitAsync` when you need the result
- **Ignore timeouts**: Set workflow timeouts for tasks that might be abandoned

## Debugging Tips

### Task Not Completing?

```csharp
// Query task status from your workflow (during debugging)
var taskInfo = await TaskWorkflowService.QueryTaskAsync(taskId);
Console.WriteLine($"Task status: Completed={taskInfo.IsCompleted}, Draft={taskInfo.CurrentDraft}");
```

### Finding Lost Tasks

```bash
# List all pending tasks
temporal workflow list --query "WorkflowType='Platform:Task Workflow'"
```

### Non-Determinism Errors?

#### Error 1: Child Workflow ID Mismatch
**Error**: `Child workflow id of scheduled event does not match...`

**Fix**: You're using `Guid.NewGuid()` instead of `Workflow.NewGuid()`. Change it and restart the workflow (you may need to terminate the existing one).

#### Error 2: Task Scheduler Mismatch
**Error**: `Workflow task failed: Non-deterministic code detected...`

**Common Causes**:
- Using `Task.Run()` → Change to `Workflow.RunTaskAsync()`
- Using `Task.Delay()` → Change to `Workflow.DelayAsync()`
- Using `Task.WhenAll()` → Change to `Workflow.WhenAllAsync()`
- Using `Task.WhenAny()` → Change to `Workflow.WhenAnyAsync()`
- Using `ConfigureAwait(false)` → Remove or change to `ConfigureAwait(true)`

**Quick Fix**: Search your workflow code for `Task.` and replace with workflow-safe equivalents.

## Real-World Example

```csharp
[Workflow("Research Agent:Content Pipeline")]
public class ContentPipelineWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string topic)
    {
        // 1. AI researches the topic
        var research = await ResearchTopicAsync(topic);
        
        // 2. AI generates first draft
        var draft = await GenerateDraftAsync(research);
        
        // 3. Human edits and approves (HITL)
        var editResult = await TaskWorkflowService.CreateAndWaitAsync(
            taskId: $"edit-{topic.ToLower().Replace(" ", "-")}",
            title: $"Edit: {topic}",
            description: "Review and polish the AI draft",
            participantId: "content-editor@company.com",
            draftWork: draft,
            metadata: new Dictionary<string, object>
            {
                { "topic", topic },
                { "wordCount", draft.Split(' ').Length },
                { "generatedAt", Workflow.UtcNow }
            }
        );
        
        if (!editResult.Success)
            return $"Content rejected: {editResult.RejectionReason}";
        
        // 4. AI optimizes for SEO
        var optimized = await OptimizeForSEOAsync(editResult.FinalWork);
        
        // 5. Final approval (HITL)
        var approvalResult = await TaskWorkflowService.CreateAndWaitAsync(
            taskId: $"approve-{topic.ToLower().Replace(" ", "-")}",
            title: $"Final Approval: {topic}",
            description: "Approve before publishing",
            participantId: "managing-editor@company.com",
            draftWork: optimized
        );
        
        if (!approvalResult.Success)
            return $"Publication rejected: {approvalResult.RejectionReason}";
        
        // 6. Publish!
        await PublishAsync(approvalResult.FinalWork);
        
        return "✅ Published successfully!";
    }
}
```

## What's Next?

- See `Xians.Lib/Workflows/Tasks/Examples.cs` for more patterns
- Check `Xians.Lib/Workflows/Tasks/SUMMARY.md` for architecture details
- Read about [Sub-Workflows](./SubWorkflows.md) for advanced composition

---

**Remember**: HITL tasks are just workflows. They're durable, recoverable, and visible. Your humans are now part of your workflow execution graph. 🎯


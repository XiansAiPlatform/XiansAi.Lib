
using Temporalio.Workflows;

public class SafeHandler
{
    protected int maxHistoryLength;

    public void ContinueAsNew(int maxHistoryLength = 1000)
    {
        this.maxHistoryLength = maxHistoryLength;
        if (ShouldContinueAsNew)
        {
            throw Workflow.CreateContinueAsNewException(Workflow.Info.WorkflowType, []);
        }
    }
    public bool ShouldContinueAsNew =>
        // Don't continue as new while update running
        Workflow.AllHandlersFinished &&
        // Continue if suggested or, for ease of testing, max history reached
        (Workflow.ContinueAsNewSuggested || Workflow.CurrentHistoryLength > maxHistoryLength);
}
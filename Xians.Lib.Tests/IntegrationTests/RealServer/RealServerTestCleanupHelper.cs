using Temporalio.Client;
using Xians.Lib.Agents.Core;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Helper class for consistent cleanup patterns in real server tests.
/// Ensures resources are properly cleaned up even when errors occur.
/// </summary>
public class RealServerTestCleanupHelper
{
    private readonly List<XiansAgent> _agents = new();
    private readonly List<string> _knowledgeItems = new();
    private readonly List<string> _documentIds = new();
    private readonly List<string> _workflowIds = new();
    private readonly Dictionary<XiansAgent, List<string>> _agentKnowledge = new();
    private readonly Dictionary<XiansAgent, List<string>> _agentDocuments = new();
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    private ITemporalClient? _temporalClient;

    /// <summary>
    /// Tracks an agent for cleanup.
    /// </summary>
    public void TrackAgent(XiansAgent agent)
    {
        if (!_agents.Contains(agent))
        {
            _agents.Add(agent);
            _agentKnowledge[agent] = new List<string>();
            _agentDocuments[agent] = new List<string>();
        }
    }

    /// <summary>
    /// Tracks knowledge for cleanup under a specific agent.
    /// </summary>
    public void TrackKnowledge(XiansAgent agent, string knowledgeName)
    {
        TrackAgent(agent);
        if (!_agentKnowledge[agent].Contains(knowledgeName))
        {
            _agentKnowledge[agent].Add(knowledgeName);
        }
    }

    /// <summary>
    /// Tracks a document for cleanup under a specific agent.
    /// </summary>
    public void TrackDocument(XiansAgent agent, string documentId)
    {
        TrackAgent(agent);
        if (!_agentDocuments[agent].Contains(documentId))
        {
            _agentDocuments[agent].Add(documentId);
        }
    }

    /// <summary>
    /// Tracks a workflow ID for cleanup.
    /// </summary>
    public void TrackWorkflow(string workflowId)
    {
        if (!_workflowIds.Contains(workflowId))
        {
            _workflowIds.Add(workflowId);
        }
    }

    /// <summary>
    /// Tracks worker cancellation token and task.
    /// </summary>
    public void TrackWorker(CancellationTokenSource cts, Task workerTask)
    {
        _workerCts = cts;
        _workerTask = workerTask;
    }

    /// <summary>
    /// Tracks Temporal client for workflow cleanup.
    /// </summary>
    public void TrackTemporalClient(ITemporalClient client)
    {
        _temporalClient = client;
    }

    /// <summary>
    /// Performs complete cleanup of all tracked resources.
    /// Safe to call multiple times - will only clean up each resource once.
    /// </summary>
    public async Task CleanupAsync()
    {
        Console.WriteLine("🧹 Starting cleanup...");

        // 1. Stop workers first (before terminating workflows)
        await StopWorkersAsync();

        // 2. Terminate workflows
        await TerminateWorkflowsAsync();

        // 3. Clean up knowledge items (per agent)
        await CleanupKnowledgeAsync();

        // 4. Clean up documents (per agent)
        await CleanupDocumentsAsync();

        // 5. Delete agents
        await DeleteAgentsAsync();

        // 6. Clear context
        ClearContext();

        Console.WriteLine("✓ Cleanup complete");
    }

    /// <summary>
    /// Stops workers if tracked.
    /// </summary>
    private async Task StopWorkersAsync()
    {
        if (_workerCts != null)
        {
            try
            {
                _workerCts.Cancel();
                if (_workerTask != null)
                {
                    try
                    {
                        await _workerTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                }
                Console.WriteLine("  ✓ Workers stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Warning: Failed to stop workers: {ex.Message}");
            }
            finally
            {
                _workerCts?.Dispose();
                _workerCts = null;
                _workerTask = null;
            }
        }
    }

    /// <summary>
    /// Terminates all tracked workflows.
    /// </summary>
    private async Task TerminateWorkflowsAsync()
    {
        if (_temporalClient == null || _workflowIds.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var workflowId in _workflowIds)
            {
                try
                {
                    await TemporalTestUtils.TerminateWorkflowIfRunningAsync(
                        _temporalClient, 
                        workflowId, 
                        "Test cleanup");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠ Warning: Failed to terminate workflow {workflowId}: {ex.Message}");
                }
            }
            Console.WriteLine($"  ✓ Terminated {_workflowIds.Count} workflow(s)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Warning: Failed to terminate workflows: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleans up all tracked knowledge items.
    /// </summary>
    private async Task CleanupKnowledgeAsync()
    {
        int totalCleaned = 0;
        
        foreach (var kvp in _agentKnowledge)
        {
            var agent = kvp.Key;
            var knowledgeNames = kvp.Value;

            foreach (var knowledgeName in knowledgeNames)
            {
                try
                {
                    await agent.Knowledge.DeleteAsync(knowledgeName);
                    totalCleaned++;
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        if (totalCleaned > 0)
        {
            Console.WriteLine($"  ✓ Cleaned up {totalCleaned} knowledge item(s)");
        }
    }

    /// <summary>
    /// Cleans up all tracked documents.
    /// </summary>
    private async Task CleanupDocumentsAsync()
    {
        int totalCleaned = 0;
        
        foreach (var kvp in _agentDocuments)
        {
            var agent = kvp.Key;
            var documentIds = kvp.Value;

            foreach (var documentId in documentIds)
            {
                try
                {
                    await agent.Documents.DeleteAsync(documentId);
                    totalCleaned++;
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        if (totalCleaned > 0)
        {
            Console.WriteLine($"  ✓ Cleaned up {totalCleaned} document(s)");
        }
    }

    /// <summary>
    /// Deletes all tracked agents.
    /// </summary>
    private async Task DeleteAgentsAsync()
    {
        foreach (var agent in _agents)
        {
            try
            {
                await agent.DeleteAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        if (_agents.Count > 0)
        {
            Console.WriteLine($"  ✓ Deleted {_agents.Count} agent(s)");
        }
    }

    /// <summary>
    /// Clears XiansContext.
    /// </summary>
    private void ClearContext()
    {
        try
        {
            XiansContext.Clear();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Helper method to safely execute cleanup operations on agents.
    /// Wraps cleanup in try-catch to ensure all cleanup attempts are made.
    /// </summary>
    public static async Task SafeCleanupAgentAsync(XiansAgent? agent)
    {
        if (agent == null) return;

        try
        {
            await agent.DeleteAsync();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Helper method to safely cleanup knowledge.
    /// </summary>
    public static async Task SafeCleanupKnowledgeAsync(XiansAgent? agent, string knowledgeName)
    {
        if (agent == null) return;

        try
        {
            await agent.Knowledge.DeleteAsync(knowledgeName);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Helper method to safely cleanup document.
    /// </summary>
    public static async Task SafeCleanupDocumentAsync(XiansAgent? agent, string documentId)
    {
        if (agent == null) return;

        try
        {
            await agent.Documents.DeleteAsync(documentId);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Helper method to safely terminate built-in workflows.
    /// </summary>
    public static async Task SafeTerminateBuiltInWorkflowsAsync(
        ITemporalClient client, 
        string agentName, 
        string[] workflowNames)
    {
        try
        {
            await TemporalTestUtils.TerminateBuiltInWorkflowsAsync(
                client, 
                agentName, 
                workflowNames);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Warning: Failed to terminate workflows: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to safely terminate custom workflows.
    /// </summary>
    public static async Task SafeTerminateCustomWorkflowsAsync(
        ITemporalClient client, 
        List<string> workflowIds, 
        string reason = "Test cleanup")
    {
        try
        {
            await TemporalTestUtils.TerminateCustomWorkflowsAsync(
                client, 
                workflowIds, 
                reason);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Warning: Failed to terminate custom workflows: {ex.Message}");
        }
    }
}

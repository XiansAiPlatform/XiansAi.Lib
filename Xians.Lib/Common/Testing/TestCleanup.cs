namespace Xians.Lib.Common.Testing;

/// <summary>
/// Centralized cleanup utility for test isolation.
/// Provides a single point to reset all static state used in tests.
/// </summary>
/// <remarks>
/// ⚠️ TESTING ONLY: This class should only be used in test projects.
/// It resets static state which would be dangerous in production.
/// </remarks>
public static class TestCleanup
{
    /// <summary>
    /// Resets all static state to ensure test isolation.
    /// Call this in test setup/teardown to prevent test contamination.
    /// </summary>
    /// <remarks>
    /// This method coordinates cleanup across all static registries and caches:
    /// - Agent and workflow registries (XiansContext)
    /// - Workflow handlers (BuiltinWorkflow)
    /// - Static activity services (KnowledgeActivities)
    /// - Server settings cache
    /// - Certificate cache
    /// </remarks>
    public static void ResetAllStaticState()
    {
        // Clear agent and workflow registries
        Agents.Core.XiansContext.CleanupForTests();
        
        // Clear workflow handlers
        Temporal.Workflows.BuiltinWorkflow.ClearHandlersForTests();
        
        // Clear static activity services
        Temporal.Workflows.Knowledge.KnowledgeActivities.ClearStaticServicesForTests();
        
        // Clear server settings cache
        Infrastructure.SettingsService.ResetCache();
        
        // Clear certificate cache
        Security.CertificateCache.Clear();
    }

    /// <summary>
    /// Resets only the workflow-related state (lighter cleanup).
    /// Use when you only need to reset workflows between tests.
    /// </summary>
    public static void ResetWorkflowState()
    {
        Agents.Core.XiansContext.Clear();
        Temporal.Workflows.BuiltinWorkflow.ClearHandlersForTests();
    }

    /// <summary>
    /// Resets only cache state.
    /// Use when you need fresh cache state without resetting registries.
    /// </summary>
    public static void ResetCaches()
    {
        Infrastructure.SettingsService.ResetCache();
        Security.CertificateCache.Clear();
    }
}

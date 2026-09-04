using System.Runtime.InteropServices;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Wires POSIX termination signals to a <see cref="CancellationTokenSource"/> so Temporal
/// workers shut down gracefully instead of being killed mid-task.
/// </summary>
/// <remarks>
/// Container orchestrators (e.g. Kubernetes) stop processes with SIGTERM on scale-down,
/// eviction, and rollout. Without a handler the process exits immediately and in-flight
/// activities are aborted; cancelling the run token instead lets each
/// <c>TemporalWorker.ExecuteAsync</c> stop polling and drain before exit.
/// </remarks>
internal static class ShutdownSignals
{
    /// <summary>
    /// Registers a SIGTERM handler that cancels <paramref name="tokenSource"/> and suppresses
    /// the default immediate process termination. Dispose the returned registration once the
    /// run completes.
    /// </summary>
    internal static IDisposable RegisterSigTerm(CancellationTokenSource tokenSource) =>
        PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => Handle(context, tokenSource));

    // Separated from the registration so the handler behavior is unit-testable —
    // PosixSignalRegistration only invokes it on a real signal.
    internal static void Handle(PosixSignalContext context, CancellationTokenSource tokenSource)
    {
        context.Cancel = true;
        tokenSource.Cancel();
    }
}

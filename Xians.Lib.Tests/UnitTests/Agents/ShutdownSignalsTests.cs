using System.Runtime.InteropServices;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// SIGTERM wiring for graceful worker shutdown: the handler must cancel the run token AND
/// suppress the default immediate process termination, and the registration must be
/// safely disposable. The handler logic is exercised directly, plus one real-signal test
/// on POSIX platforms (where CI runs) that raises SIGTERM in-process — surviving it proves
/// the registration suppressed the default termination.
/// </summary>
public class ShutdownSignalsTests
{
    private const int SigtermNumber = 15;

    [DllImport("libc", EntryPoint = "raise")]
    private static extern int Raise(int signal);

    [Fact]
    public void RegisterSigTerm_RealSignal_CancelsToken_AndProcessSurvives()
    {
        // On Windows, SIGTERM maps to console-control events that cannot be raised in-process;
        // libc raise() is POSIX-only. CI runs on ubuntu-latest, so this path is covered there.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var tokenSource = new CancellationTokenSource();
        using var registration = ShutdownSignals.RegisterSigTerm(tokenSource);

        Assert.Equal(0, Raise(SigtermNumber));

        // Signal handlers run on a dedicated runtime thread — wait instead of asserting inline.
        var cancelled = tokenSource.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));

        // Reaching the asserts at all means context.Cancel = true suppressed process exit;
        // a broken handler would have torn down the whole test host here.
        Assert.True(cancelled, "SIGTERM was raised but the run token was not cancelled within 10s");
    }
    [Fact]
    public void Handle_CancelsTokenSource_AndSuppressesDefaultTermination()
    {
        using var tokenSource = new CancellationTokenSource();
        var context = new PosixSignalContext(PosixSignal.SIGTERM);

        ShutdownSignals.Handle(context, tokenSource);

        Assert.True(tokenSource.IsCancellationRequested);
        Assert.True(context.Cancel);
    }

    [Fact]
    public void RegisterSigTerm_ReturnsRegistration_WithoutCancellingToken()
    {
        using var tokenSource = new CancellationTokenSource();

        using var registration = ShutdownSignals.RegisterSigTerm(tokenSource);

        Assert.NotNull(registration);
        Assert.False(tokenSource.IsCancellationRequested);
    }

    [Fact]
    public void RegisterSigTerm_DoubleDispose_DoesNotThrow()
    {
        using var tokenSource = new CancellationTokenSource();
        var registration = ShutdownSignals.RegisterSigTerm(tokenSource);

        registration.Dispose();
        registration.Dispose();
    }
}

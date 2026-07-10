namespace Xians.Lib.Common.Infrastructure;

/// <summary>
/// Serializes multi-line console display blocks (boxes) so that output from
/// concurrently running agents does not interleave. Console.Write is thread-safe
/// per call, but the SDK's display boxes are composed of many small writes with
/// color changes, so the whole block must be printed under a single lock.
/// </summary>
internal static class ConsoleDisplay
{
    private static readonly object _lock = new();

    /// <summary>
    /// Executes the given display action while holding a process-wide console lock,
    /// guaranteeing the block is printed without interleaving from other display blocks.
    /// Always resets the console color afterwards, even if the action throws.
    /// </summary>
    internal static void WriteBlock(Action displayAction)
    {
        lock (_lock)
        {
            try
            {
                displayAction();
            }
            finally
            {
                Console.ResetColor();
            }
        }
    }
}

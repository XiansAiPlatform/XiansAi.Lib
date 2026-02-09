using Microsoft.Extensions.Logging;
using Xians.Lib.Logging;
using Xians.Lib.Common.Infrastructure;

/// <summary>
/// Simple test to verify that ServerLogLevel filtering works correctly.
/// 
/// Run this test to confirm that when ServerLogLevel = Information,
/// only Information and above logs are enqueued for upload.
/// </summary>
public class LogLevelFilteringTest
{
    public static void TestFiltering()
    {
        Console.WriteLine("=== Log Level Filtering Test ===\n");
        
        // Configure server log level to Information
        LoggerFactory.ConfigureLogLevels(
            consoleLogLevel: LogLevel.Debug,    // Console shows all logs
            serverLogLevel: LogLevel.Information // Server only gets Information+
        );
        
        Console.WriteLine($"ServerLogLevel configured to: Information (enum value 2)");
        Console.WriteLine($"Actual ServerLogLevel: {LoggerFactory.GetServerLogLevel()}\n");
        
        // Create an API logger instance
        var logger = new ApiLogger();
        
        // Test each log level
        var testCases = new[]
        {
            (LogLevel.Trace, "Trace log - should be filtered"),
            (LogLevel.Debug, "Debug log - should be filtered"),
            (LogLevel.Information, "Information log - should be uploaded"),
            (LogLevel.Warning, "Warning log - should be uploaded"),
            (LogLevel.Error, "Error log - should be uploaded"),
            (LogLevel.Critical, "Critical log - should be uploaded")
        };
        
        Console.WriteLine("Testing IsEnabled() for each log level:\n");
        foreach (var (level, message) in testCases)
        {
            var enabled = logger.IsEnabled(level);
            var status = enabled ? "✅ ENABLED" : "❌ FILTERED";
            var enumValue = (int)level;
            Console.WriteLine($"  {level,-12} (value={enumValue}): {status}");
        }
        
        Console.WriteLine("\n=== Expected Results ===");
        Console.WriteLine("✅ Information, Warning, Error, Critical should be ENABLED");
        Console.WriteLine("❌ Trace and Debug should be FILTERED");
        
        Console.WriteLine("\n=== Verification ===");
        bool traceFiltered = !logger.IsEnabled(LogLevel.Trace);
        bool debugFiltered = !logger.IsEnabled(LogLevel.Debug);
        bool infoEnabled = logger.IsEnabled(LogLevel.Information);
        bool warningEnabled = logger.IsEnabled(LogLevel.Warning);
        bool errorEnabled = logger.IsEnabled(LogLevel.Error);
        bool criticalEnabled = logger.IsEnabled(LogLevel.Critical);
        
        bool allTestsPassed = traceFiltered && debugFiltered && 
                             infoEnabled && warningEnabled && 
                             errorEnabled && criticalEnabled;
        
        if (allTestsPassed)
        {
            Console.WriteLine("✅ ALL TESTS PASSED - Filtering works correctly!");
        }
        else
        {
            Console.WriteLine("❌ TESTS FAILED - Filtering is not working correctly!");
            Console.WriteLine("\nFailures:");
            if (!traceFiltered) Console.WriteLine("  - Trace should be filtered but isn't");
            if (!debugFiltered) Console.WriteLine("  - Debug should be filtered but isn't");
            if (!infoEnabled) Console.WriteLine("  - Information should be enabled but isn't");
            if (!warningEnabled) Console.WriteLine("  - Warning should be enabled but isn't");
            if (!errorEnabled) Console.WriteLine("  - Error should be enabled but isn't");
            if (!criticalEnabled) Console.WriteLine("  - Critical should be enabled but isn't");
        }
    }
}

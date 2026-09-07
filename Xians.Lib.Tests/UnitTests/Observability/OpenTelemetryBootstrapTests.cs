using Xians.Lib.Observability;

namespace Xians.Lib.Tests.UnitTests.Observability;

public class OpenTelemetryBootstrapTests
{
    [Fact]
    public void TryInitialize_WhenDisabled_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("OpenTelemetry__Enabled", "false");
        Environment.SetEnvironmentVariable("OpenTelemetry__OtlpEndpoint", "http://localhost:4317");
        try
        {
            OpenTelemetryBootstrap.Shutdown();
            Assert.False(OpenTelemetryBootstrap.TryInitialize());
        }
        finally
        {
            OpenTelemetryBootstrap.Shutdown();
            Environment.SetEnvironmentVariable("OpenTelemetry__Enabled", null);
            Environment.SetEnvironmentVariable("OpenTelemetry__OtlpEndpoint", null);
        }
    }

    [Fact]
    public void TryInitialize_WhenEnabledButMissingEndpoint_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("OpenTelemetry__Enabled", "true");
        Environment.SetEnvironmentVariable("OpenTelemetry__OtlpEndpoint", null);
        try
        {
            OpenTelemetryBootstrap.Shutdown();
            Assert.False(OpenTelemetryBootstrap.TryInitialize());
        }
        finally
        {
            OpenTelemetryBootstrap.Shutdown();
            Environment.SetEnvironmentVariable("OpenTelemetry__Enabled", null);
        }
    }
}

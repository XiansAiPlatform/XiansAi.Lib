namespace Xians.Lib.Tests.UnitTests.Logging;

/// <summary>
/// Serializes test classes that mutate process-wide logging state: the static
/// <see cref="Xians.Lib.Common.Infrastructure.LoggerFactory"/> (Reset/ConfigureLogLevels
/// overrides) and the log-level environment variables (e.g. API_LOG_LEVEL).
/// </summary>
[CollectionDefinition(Name)]
public class LoggerStaticStateCollection
{
    public const string Name = "LoggerStaticState";
}

using Microsoft.Extensions.Logging;

public static class Globals
{
    public static XiansAIConfig? XiansAIConfig;

    public static ILoggerFactory LogFactory = 
        LoggerFactory.Create(builder => builder.AddConsole());
}

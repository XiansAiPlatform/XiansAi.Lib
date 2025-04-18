using System;
using System.Collections.Generic;

namespace XiansAi.Router.Plugins;

/// <summary>
/// Provides a list of predefined plugins that are available for use with the DynamicOrchestrator.
/// Only the plugins defined within this class can be added to the orchestrator.
/// </summary>
public static class AvailablePlugins
{
    /// <summary>
    /// The DatePlugin provides functionalities related to date and time.
    /// </summary>
    public static readonly Type DatePlugin = typeof(DatePlugin);


    /// <summary>
    /// Gets all the available plugins as an enumerable collection.
    /// </summary>
    public static IEnumerable<Type> All => new List<Type> { 
        DatePlugin
        // , AnotherPlugins
    };
}


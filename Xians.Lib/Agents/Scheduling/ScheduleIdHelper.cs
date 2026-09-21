namespace Xians.Lib.Agents.Scheduling;

/// <summary>
/// Helper class for building schedule identifiers.
/// </summary>
internal static class ScheduleIdHelper
{
    /// <summary>
    /// Builds the full schedule ID using the pattern: tenantId:agentName:idPostfix:scheduleId
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="activationName">The activation name.</param>
    /// <param name="scheduleName">The schedule identifier.</param>
    /// <returns>The fully qualified schedule ID.</returns>
    public static string BuildFullScheduleId(string tenantId, string agentName, string? activationName, string scheduleName)
    {
        return $"{tenantId}:{agentName}{(activationName is not null ? $":{activationName}" : string.Empty)}:{scheduleName}";
    }

    /// <summary>
    /// Escapes a string for use as a quoted Temporal visibility-query literal.
    /// </summary>
    public static string EscapeVisibilityLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    public static string BuildFullWorkflowId(string tenantId, string workflowType, string activationName)
    {
        return $"{tenantId}:{workflowType}:{activationName}";
    }
}

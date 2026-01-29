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
    /// <param name="idPostfix">The ID postfix.</param>
    /// <param name="scheduleId">The schedule identifier.</param>
    /// <returns>The fully qualified schedule ID.</returns>
    public static string BuildFullScheduleId(string tenantId, string agentName, string idPostfix, string scheduleId)
    {
        return $"{tenantId}:{agentName}:{idPostfix}:{scheduleId}";
    }
}

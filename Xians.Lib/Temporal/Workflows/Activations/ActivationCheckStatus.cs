namespace Xians.Lib.Temporal.Workflows.Activations;

/// <summary>
/// Result of an activation existence check performed by
/// <see cref="ActivationActivities.ValidateActivationAsync"/>.
/// Returned as a value (instead of throwing) so a definitive negative result completes the
/// activity successfully - Temporal logs every failed activity at Warning level with a full
/// stack trace, which is just noise for an expected "not found" outcome. The workflow-side
/// caller converts non-<see cref="Active"/> statuses into the typed activation exceptions.
/// </summary>
public enum ActivationCheckStatus
{
    /// <summary>The activation exists and is active.</summary>
    Active = 0,

    /// <summary>The activation does not exist for the agent in the acting tenant.</summary>
    NotFound = 1,

    /// <summary>The activation exists but has been deactivated.</summary>
    Deactivated = 2,
}

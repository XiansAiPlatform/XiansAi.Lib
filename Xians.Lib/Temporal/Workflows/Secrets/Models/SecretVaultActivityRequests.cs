namespace Xians.Lib.Temporal.Workflows.Secrets.Models;

/// <summary>
/// Scope dimensions forwarded with Secret Vault activity calls.
/// </summary>
public class SecretVaultScopePayload
{
    public string? TenantId { get; set; }
    public string? AgentId { get; set; }
    public string? UserId { get; set; }
    public string? ActivationName { get; set; }
}

/// <summary>Activity payload for fetching a secret by key.</summary>
public class SecretVaultFetchActivityRequest
{
    public required string Key { get; set; }
    public SecretVaultScopePayload Scope { get; set; } = new();
}

/// <summary>Activity payload for listing secrets.</summary>
public class SecretVaultListActivityRequest
{
    public SecretVaultScopePayload Scope { get; set; } = new();
}

/// <summary>Activity payload for get-by-id / delete.</summary>
public class SecretVaultIdActivityRequest
{
    public required string Id { get; set; }
    public SecretVaultScopePayload Scope { get; set; } = new();
}

/// <summary>Activity payload for updating a secret.</summary>
public class SecretVaultUpdateActivityRequest
{
    public required string Id { get; set; }
    public string? Value { get; set; }
    public object? AdditionalData { get; set; }
    public SecretVaultScopePayload Scope { get; set; } = new();
}

using System.Text.Json.Serialization;

namespace Xians.Lib.Agents.Secrets.Models;

/// <summary>
/// Request body for creating a secret via the Agent Secret Vault API.
/// </summary>
public class SecretVaultCreateRequest
{
    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("agentId")]
    public string? AgentId { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    /// <summary>Optional. Flat key-value metadata; values must be string, number, or boolean only.</summary>
    [JsonPropertyName("additionalData")]
    public object? AdditionalData { get; set; }
}

/// <summary>
/// Request body for updating a secret via the Agent Secret Vault API.
/// </summary>
public class SecretVaultUpdateRequest
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("agentId")]
    public string? AgentId { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("additionalData")]
    public object? AdditionalData { get; set; }
}

/// <summary>
/// Full secret record returned by create, get-by-id, and update (value is decrypted).
/// </summary>
public class SecretVaultGetResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("agentId")]
    public string? AgentId { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("additionalData")]
    public object? AdditionalData { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("createdBy")]
    public required string CreatedBy { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("updatedBy")]
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Response from fetch-by-key: decrypted value and optional additionalData only.
/// </summary>
public class SecretVaultFetchResponse
{
    [JsonPropertyName("value")]
    public required string Value { get; set; }

    [JsonPropertyName("additionalData")]
    public object? AdditionalData { get; set; }
}

/// <summary>
/// List item for secrets (no decrypted value).
/// </summary>
public class SecretVaultListItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("agentId")]
    public string? AgentId { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("additionalData")]
    public object? AdditionalData { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("createdBy")]
    public required string CreatedBy { get; set; }
}

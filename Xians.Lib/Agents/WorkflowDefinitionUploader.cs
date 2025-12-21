using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Models;
using Xians.Lib.Http;

namespace Xians.Lib.Agents;

/// <summary>
/// Service for uploading workflow definitions to the server.
/// </summary>
internal class WorkflowDefinitionUploader
{
    private readonly IHttpClientService _httpService;
    private readonly ILogger<WorkflowDefinitionUploader>? _logger;
    private static readonly HashSet<string> _uploadedDefinitions = new();
    private static readonly object _uploadLock = new();
    
    private const string API_ENDPOINT = "/api/agent/definitions";

    public WorkflowDefinitionUploader(IHttpClientService httpService, ILogger<WorkflowDefinitionUploader>? logger = null)
    {
        _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
        _logger = logger;
    }

    /// <summary>
    /// Uploads a workflow definition to the server.
    /// </summary>
    public async Task UploadWorkflowDefinitionAsync(WorkflowDefinition definition)
    {
        var workflowKey = $"{definition.Agent}:{definition.WorkflowType}:{definition.SystemScoped}";
        
        // Check if already uploaded in this session
        lock (_uploadLock)
        {
            if (_uploadedDefinitions.Contains(workflowKey))
            {
                _logger?.LogDebug("Workflow definition for {WorkflowType} already uploaded in this session, skipping", definition.WorkflowType);
                return;
            }
        }
        
        _logger?.LogDebug("Uploading workflow definition for {WorkflowType} to server...", definition.WorkflowType);
        
        try
        {
            await UploadToServerAsync(definition);
            
            // Mark as uploaded
            lock (_uploadLock)
            {
                _uploadedDefinitions.Add(workflowKey);
            }
            
            _logger?.LogInformation("Successfully uploaded workflow definition for {WorkflowType}", definition.WorkflowType);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to upload workflow definition for {WorkflowType}", definition.WorkflowType);
            throw;
        }
    }

    /// <summary>
    /// Uploads the workflow definition to the server with hash checking.
    /// </summary>
    private async Task UploadToServerAsync(WorkflowDefinition definition)
    {
        var response = await _httpService.ExecuteWithRetryAsync(async () =>
        {
            var client = await _httpService.GetHealthyClientAsync();
            
            // Serialize and compute hash
            var serializedDefinition = JsonSerializer.Serialize(definition);
            var hash = ComputeHash(serializedDefinition);

            // Check if definition is already up to date
            var checkUrl = $"{API_ENDPOINT}/check?workflowType={Uri.EscapeDataString(definition.WorkflowType)}&systemScoped={definition.SystemScoped}&hash={hash}";
            var hashCheckResponse = await client.GetAsync(checkUrl);

            switch (hashCheckResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                    _logger?.LogInformation("Workflow definition for {WorkflowType} already up to date on server", definition.WorkflowType);
                    return hashCheckResponse;

                case HttpStatusCode.NotFound:
                    // Proceed with upload
                    _logger?.LogDebug("Workflow definition not found or outdated, uploading...");
                    break;

                default:
                    var error = await hashCheckResponse.Content.ReadAsStringAsync();
                    _logger?.LogError("Hash check failed with status {StatusCode}: {Error}", hashCheckResponse.StatusCode, error);
                    throw new InvalidOperationException($"Hash check failed: {error}");
            }

            // Upload the definition
            var uploadResponse = await client.PostAsync(API_ENDPOINT, JsonContent.Create(definition));
            
            if (uploadResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorMessage = await uploadResponse.Content.ReadAsStringAsync();
                _logger?.LogError("Server rejected workflow definition: {Error}", errorMessage);
                throw new InvalidOperationException($"Server rejected workflow definition: {errorMessage}");
            }
            
            uploadResponse.EnsureSuccessStatusCode();
            return uploadResponse;
        });
    }

    /// <summary>
    /// Computes a SHA256 hash of the input string.
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Resets the uploaded definitions cache (primarily for testing).
    /// </summary>
    public static void ResetCache()
    {
        lock (_uploadLock)
        {
            _uploadedDefinitions.Clear();
        }
    }
}


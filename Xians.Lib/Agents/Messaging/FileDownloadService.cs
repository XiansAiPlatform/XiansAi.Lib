using Microsoft.Extensions.Logging;
using Xians.Lib.Common;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Downloads message file attachments stored out-of-band (GridFS) from the Xians platform.
/// File messages carry only references (fileId + metadata); the bytes are fetched on demand
/// so <see cref="UploadedFile.Content"/> / <see cref="UploadedFile.GetBytes"/> keep working.
/// </summary>
internal class FileDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public FileDownloadService(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Downloads the raw bytes of a stored file by its id, scoped to the given tenant.
    /// </summary>
    public async Task<byte[]> DownloadAsync(string fileId, string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fileId))
        {
            throw new ArgumentException("fileId is required", nameof(fileId));
        }

        var endpoint = $"{WorkflowConstants.ApiEndpoints.Files}/{Uri.EscapeDataString(fileId)}";

        _logger.LogDebug("Downloading file {FileId} from {Endpoint}", fileId, endpoint);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrEmpty(tenantId))
        {
            httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, tenantId);
        }

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to download file {FileId}: StatusCode={StatusCode}, Error={Error}",
                fileId, response.StatusCode, error);
            throw new HttpRequestException(
                $"Failed to download file '{fileId}'. Status: {response.StatusCode}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}

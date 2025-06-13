using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace XiansAi.Messaging;

public class KeycloakService
{
    private readonly string _jwksUrl;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly ILogger _logger;
    private HttpClient? _httpClient;

    public KeycloakService(HttpClient? httpClient = null)
    {
        _logger = Globals.LogFactory.CreateLogger<KeycloakService>();
        _httpClient = httpClient;
        _jwksUrl = PlatformConfig.KEYCLOAK_JWKS_URL ?? throw new InvalidOperationException("KEYCLOAK_JWKS_URL not set");
        _issuer = PlatformConfig.KEYCLOAK_ISSUER ?? throw new InvalidOperationException("KEYCLOAK_ISSUER not set");
        _audience = PlatformConfig.KEYCLOAK_CLIENT_ID ?? throw new InvalidOperationException("KEYCLOAK_CLIENT_ID not set");
    }

    public async Task<ClaimsPrincipal> ValidateTokenAsync(string bearerToken)
    {
        try
        {
            // Remove "Bearer " prefix if present
            string token = bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? bearerToken[7..].Trim()
                : bearerToken;

            var handler = new JwtSecurityTokenHandler();

            _logger.LogInformation("Starting to fetch signing keys...");
            var keysTask = GetSigningKeysAsync();
            var keys = await keysTask.ConfigureAwait(false);
            _logger.LogInformation("Signing keys retrieved successfully");

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudiences = _audience.Split(',').Select(a => a.Trim()).ToArray(),
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
                ValidateLifetime = true
            };

            var principal = handler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Token validation failed: {ex.Message}");
            throw;
        }
    }

    private async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync()
    {
        // Create a new HttpClient if not provided
        bool shouldDisposeClient = false;
        try
        {
            if (this._httpClient == null)
            {
                this._httpClient = new HttpClient();
                this._httpClient.Timeout = TimeSpan.FromSeconds(30);
                shouldDisposeClient = true;
            }

            try
            {
                _logger.LogInformation($"Fetching JWKS from: {_jwksUrl}");
                HttpResponseMessage jwksResponse = await _httpClient.GetAsync(_jwksUrl)
                    .ConfigureAwait(false);
                string jwksContent = await jwksResponse.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);

                var keys = new JsonWebKeySet(jwksContent);
                return keys.Keys;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to fetch JWKS: {ex.Message}");
                throw;
            }
        }
        finally
        {
            if (shouldDisposeClient && _httpClient != null)
            {
                _httpClient.Dispose();
                _httpClient = null;
            }
        }
    }


}
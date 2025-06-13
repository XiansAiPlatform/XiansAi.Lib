using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace XiansAi.Messaging;

public class Auth0Service
{
    private readonly string _jwksUrl;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly ILogger _logger;
    private HttpClient? _httpClient;

    public Auth0Service(HttpClient? httpClient = null)
    {
        _logger = Globals.LogFactory.CreateLogger<Auth0Service>();
        _httpClient = httpClient;
        _jwksUrl = PlatformConfig.AUTH0_JWKS_URL ?? throw new InvalidOperationException("AUTH0_JWKS_URL not set");
        _issuer = PlatformConfig.AUTH0_ISSUER ?? throw new InvalidOperationException("AUTH0_ISSUER not set");
        _audience = PlatformConfig.AUTH0_AUDIENCE ?? throw new InvalidOperationException("AUTH0_AUDIENCE not set");
    }

    public async Task<ClaimsPrincipal> ValidateTokenAsync(string bearerToken)
    {
        try
        {
            _logger.LogInformation("Starting Auth0 token validation");

            // Remove "Bearer " prefix if present
            string token = bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? bearerToken[7..].Trim()
                : bearerToken;

            var handler = new JwtSecurityTokenHandler();
            var keys = await GetSigningKeysAsync();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudiences = _audience.Split(',').Select(a => a.Trim()).ToArray(), // Handle multiple audiences
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
                ValidateLifetime = true
            };

            var principal = handler.ValidateToken(token, validationParameters, out _);
            _logger.LogInformation("Auth0 token validation successful");
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth0 token validation failed");
            throw;
        }
    }

    private async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync()
    {
        try
        {
            // Create a new HttpClient if not provided
            bool shouldDisposeClient = false;
            if (this._httpClient == null)
            {
                this._httpClient = new HttpClient();
                this._httpClient.Timeout = TimeSpan.FromSeconds(30);
                shouldDisposeClient = true;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                _logger.LogInformation($"Fetching Auth0 JWKS from: {_jwksUrl}");

                var jwksResponse = await this._httpClient.GetAsync(_jwksUrl, cts.Token)
                    .ConfigureAwait(false);

                jwksResponse.EnsureSuccessStatusCode();

                var jwksContent = await jwksResponse.Content.ReadAsStringAsync()
                    .ConfigureAwait(false);

                if (string.IsNullOrEmpty(jwksContent))
                {
                    throw new InvalidOperationException("Empty JWKS response received from Auth0");
                }

                var keys = new JsonWebKeySet(jwksContent);
                _logger.LogInformation($"Successfully retrieved {keys.Keys.Count()} Auth0 signing keys");
                return keys.Keys;
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
        catch (OperationCanceledException)
        {
            _logger.LogError("Auth0 JWKS request timed out");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Auth0 JWKS");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Auth0 JWKS");
            throw;
        }
    }
}
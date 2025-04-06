using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace XiansAi.Http;
public class SecureApi
{
    private readonly HttpClient _client;
    private readonly X509Certificate2 _clientCertificate;
    private static SecureApi? _instance;
    private static readonly object _lock = new object();
    private readonly ILogger _logger;

    private SecureApi(string certPath, string serverUrl)
    {
        _client = new HttpClient();
        _client.BaseAddress = new Uri(serverUrl);
        _logger = Globals.LogFactory.CreateLogger<SecureApi>();

        // Load the certificate based on whether password is provided (pfx) or not (pem)
        var pemBytes = Convert.FromBase64String(certPath);
        #pragma warning disable SYSLIB0057 // Type or member is obsolete    
        _clientCertificate = new X509Certificate2(pemBytes);
        #pragma warning restore SYSLIB0057 // Type or member is obsolete

        // Export and add certificate to headers regardless of type
        var certBytes = _clientCertificate.Export(X509ContentType.Cert);
        var certBase64 = Convert.ToBase64String(certBytes);
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {certBase64}");
    }

    public static SecureApi Initialize(string certPath, string serverUrl)
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                _instance ??= new SecureApi(certPath, serverUrl);
            }
        }
        return _instance;
    }

    public static HttpClient GetClient()
    {
        if (_instance == null)
        {
            throw new InvalidOperationException("SecureApi must be initialized before getting client");
        }
        return _instance._client;
    }

    public static bool IsReady()
    {
        return _instance != null;
    }
}
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace XiansAi.Http;
public class SecureApi
{
    private readonly HttpClient _client;
    private readonly X509Certificate2 _clientCertificate;
    private static SecureApi? _instance;
    private static readonly object _lock = new object();

    private SecureApi(string certPath, string serverUrl, string? certPassword)
    {
        _client = new HttpClient();
        _client.BaseAddress = new Uri(serverUrl);

        // Load the certificate based on whether password is provided (pfx) or not (pem)
        if (!string.IsNullOrEmpty(certPassword))
        {
            // Handle .pfx file with password
            #pragma warning disable SYSLIB0057 // Type or member is obsolete
            _clientCertificate = new X509Certificate2(certPath, certPassword);
            #pragma warning restore SYSLIB0057 // Type or member is obsolete
        }
        else
        {
            // Handle .pem file
            var pemContents = File.ReadAllText(certPath);
            
            // Extract just the certificate portion between BEGIN and END markers
            var certMatch = Regex.Match(pemContents, 
                @"-----BEGIN CERTIFICATE-----\s*([^-]+)\s*-----END CERTIFICATE-----");
            
            if (!certMatch.Success || certMatch.Groups.Count < 2)
            {
                throw new InvalidOperationException("Invalid certificate format. Expected PEM format with BEGIN/END markers.");
            }

            // Get just the Base64 content and clean it
            var pemBase64 = certMatch.Groups[1].Value
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();

            var pemBytes = Convert.FromBase64String(pemBase64);
            #pragma warning disable SYSLIB0057 // Type or member is obsolete    
            _clientCertificate = new X509Certificate2(pemBytes);
            #pragma warning restore SYSLIB0057 // Type or member is obsolete
        }

        // Export and add certificate to headers regardless of type
        var certBytes = _clientCertificate.Export(X509ContentType.Cert);
        var certBase64 = Convert.ToBase64String(certBytes);
        _client.DefaultRequestHeaders.Add("X-Client-Cert", certBase64);
    }

    public static SecureApi Initialize(string certPath, string serverUrl, string? certPassword = null)
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                _instance ??= new SecureApi(certPath, serverUrl, certPassword);
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
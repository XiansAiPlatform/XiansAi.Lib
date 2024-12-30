using System.Security.Cryptography.X509Certificates;

public class SecureApi
{
    private readonly HttpClient _client;
    private readonly X509Certificate2 _clientCertificate;

    public SecureApi(string certPath, string certPassword)
    {
        // Regular HTTP client without SSL/TLS requirements
        _client = new HttpClient();

        // Load the certificate for identity purposes

#pragma warning disable SYSLIB0057 // Type or member is obsolete
        _clientCertificate = new X509Certificate2(certPath, certPassword);
#pragma warning restore SYSLIB0057 // Type or member is obsolete
        var certBytes = _clientCertificate.Export(X509ContentType.Cert);
        var certBase64 = Convert.ToBase64String(certBytes);
        _client.DefaultRequestHeaders.Add("X-Client-Cert", certBase64);
    }

    public HttpClient GetClient()
    {
        return _client;
    }

}
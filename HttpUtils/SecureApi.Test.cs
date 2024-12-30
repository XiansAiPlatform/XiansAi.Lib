using Xunit;
using System.Net.Http;
using System.IO;
using System.Net;

public class SecureApiTests
{
    private readonly string _testCertPath = "/Users/hasithy/Downloads/flowmaxer.pfx";
    private readonly string _testCertPassword = "flowpwd";

    public SecureApiTests()
    {

    }

    /*
    dotnet test --filter "FullyQualifiedName~SecureApiTests.Constructor_ValidCertificate_CreatesClientWithCertificate"
    */
    [Fact]
    public async Task Constructor_ValidCertificate_CreatesClientWithCertificate()
    {
        // Arrange
        var url = "http://localhost:5257/api/server/debug/certificate";

        // Act
        var api = new SecureApi(_testCertPath, _testCertPassword);
        var client = api.GetClient();

        // Assert
        Assert.NotNull(client);

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine(content);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CanAuthenticateWithCertificate()
    {
        // Arrange
        var baseUrl = "http://localhost:5257/";
        var api = new SecureApi(_testCertPath, baseUrl);
        var client = api.GetClient();

        // Act
        var response = await client.GetAsync("/debug/claims");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Authenticated with certificate", content);
    }

}

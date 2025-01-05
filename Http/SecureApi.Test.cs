using Xunit;
using System.Net;

namespace XiansAi.Http;

public class SecureApiTests
{
    private readonly string _testCertPath = "/Users/hasithy/Downloads/xians-ai.pfx";
    private readonly string _testCertPassword = "test";

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
        var url = "api/server/debug/certificate";

        // Act
        SecureApi.Initialize(_testCertPath, _testCertPassword, "http://localhost:5257");
        var client = SecureApi.GetClient();

        // Assert
        Assert.NotNull(client);

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine(content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }


    /*
    dotnet test --filter "FullyQualifiedName~SecureApiTests.GetLatestInstruction"
    */
    [Fact]
    public async Task GetLatestInstruction()
    {
        // Arrange
        SecureApi.Initialize(_testCertPath, _testCertPassword, "http://localhost:5257");
        var client = SecureApi.GetClient();

        // Act
        var name = "HowToIdentifyProductCompanies";
        var encodedName = WebUtility.UrlEncode(name);
        var url = $"api/server/instructions/latest?name={encodedName}";   
        Console.WriteLine( "url: " + url);
        var response = await client.GetAsync(url);
        //var content = await response.Content.ReadAsStringAsync();
        var content = await response.Content.ReadAsStringAsync();

        var tagToReplace = "{{company-name}}";
        var tagToReplaceWith = "Xians AI";
        content = content.Replace(tagToReplace, tagToReplaceWith);

        
        Console.WriteLine( "content: " + content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }


}
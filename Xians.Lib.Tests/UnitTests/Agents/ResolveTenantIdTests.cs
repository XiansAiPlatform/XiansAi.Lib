using Moq;
using Xians.Lib.Agents.Core;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Tenant resolution policy: context wins for every agent, and the certificate is a fallback only for
/// tenant-scoped agents. A system-scoped agent serves many tenants, so its certificate names the key
/// owner rather than the tenant being operated on.
///
/// dotnet test --filter "FullyQualifiedName~ResolveTenantId"
/// </summary>
[Collection("Sequential")]
public class ResolveTenantIdTests : IDisposable
{
    private const string AGENT_NAME = "test-agent";
    private const string CERTIFICATE_TENANT = "certificate-tenant";
    private const string CONTEXT_TENANT = "context-tenant";

    public ResolveTenantIdTests()
    {
        XiansContext.CleanupForTests();
        XiansContext.ClearTenantId();
    }

    public void Dispose()
    {
        XiansContext.ClearTenantId();
        XiansContext.CleanupForTests();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TenantScoped_WithoutContext_FallsBackToCertificate()
    {
        var agent = CreateAgent(systemScoped: false);

        Assert.Equal(CERTIFICATE_TENANT, XiansContext.ResolveTenantId(agent));
    }

    [Fact]
    public void TenantScoped_WithContext_PrefersContext()
    {
        var agent = CreateAgent(systemScoped: false);
        XiansContext.SetTenantId(CONTEXT_TENANT);

        // Creating and managing a schedule must agree on the tenant; reading the certificate directly
        // would make creation ignore the context that management honours.
        Assert.Equal(CONTEXT_TENANT, XiansContext.ResolveTenantId(agent));
    }

    [Fact]
    public void SystemScoped_WithContext_UsesContext()
    {
        var agent = CreateAgent(systemScoped: true);
        XiansContext.SetTenantId(CONTEXT_TENANT);

        Assert.Equal(CONTEXT_TENANT, XiansContext.ResolveTenantId(agent));
    }

    [Fact]
    public void SystemScoped_WithoutContext_ThrowsAndNeverUsesCertificate()
    {
        var agent = CreateAgent(systemScoped: true);

        var ex = Assert.Throws<InvalidOperationException>(() => XiansContext.ResolveTenantId(agent));

        Assert.DoesNotContain(CERTIFICATE_TENANT, ex.Message);
        Assert.Contains("system-scoped", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NullAgent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => XiansContext.ResolveTenantId(null!));
        Assert.Throws<ArgumentNullException>(() => XiansContext.TryResolveTenantId(null!));
    }

    [Fact]
    public void TryResolve_SystemScoped_WithoutContext_ReturnsNullRatherThanCertificate()
    {
        var agent = CreateAgent(systemScoped: true);

        Assert.Null(XiansContext.TryResolveTenantId(agent));
    }

    [Fact]
    public void TryResolve_TenantScoped_WithoutContext_FallsBackToCertificate()
    {
        var agent = CreateAgent(systemScoped: false);

        Assert.Equal(CERTIFICATE_TENANT, XiansContext.TryResolveTenantId(agent));
    }

    private static XiansAgent CreateAgent(bool systemScoped)
    {
        var mockHttpService = new Mock<IHttpClientService>();
        var mockTemporalService = new Mock<ITemporalClientService>();

        var options = new XiansOptions
        {
            ApiKey = TestCertificateGenerator.GenerateTestCertificateBase64(CERTIFICATE_TENANT, "test-user"),
            ServerUrl = "http://localhost"
        };

        return new XiansAgent(
            AGENT_NAME,
            systemScoped,
            null, null, null, null, null, null, null,
            mockTemporalService.Object,
            mockHttpService.Object,
            options,
            null);
    }
}

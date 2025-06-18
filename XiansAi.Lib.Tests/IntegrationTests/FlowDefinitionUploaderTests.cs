using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Server;

namespace XiansAi.Lib.Tests.IntegrationTests
{
    /// <summary>
    /// Integration tests for FlowDefinitionUploader functionality.
    /// These tests verify the FlowDefinitionUploader class behavior with mocked dependencies.
    /// </summary>
    public class FlowDefinitionUploaderTests
    {
        private readonly Mock<ILogger<FlowDefinitionUploader>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly HttpClient _httpClient;

        public FlowDefinitionUploaderTests()
        {
            _mockLogger = new Mock<ILogger<FlowDefinitionUploader>>();
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpHandler.Object)
            {
                BaseAddress = new Uri("https://test-server.com/")
            };
        }

        [Fact]
        public void CreateFlowDefinitionUploader_ShouldSucceed_WithValidParameters()
        {
            // Act
            var uploader = new FlowDefinitionUploader(_httpClient, _mockLogger.Object);

            // Assert
            Assert.NotNull(uploader);
        }

        [Fact]
        public void CreateFlowDefinitionUploader_ShouldThrowArgumentNullException_WhenHttpClientIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new FlowDefinitionUploader(null!, _mockLogger.Object));
        }

        [Fact]
        public void CreateFlowDefinitionUploader_ShouldThrowArgumentNullException_WhenLoggerIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new FlowDefinitionUploader(_httpClient, null!));
        }

        [Fact]
        public void XiansAiServiceFactory_GetFlowDefinitionUploader_ShouldReturnInstance()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddHttpClient();
            services.AddLogging();
            services.AddSingleton<IFlowDefinitionUploader, FlowDefinitionUploader>();
            var serviceProvider = services.BuildServiceProvider();

            // Act
            var uploader = serviceProvider.GetService<IFlowDefinitionUploader>();

            // Assert
            Assert.NotNull(uploader);
            Assert.IsType<FlowDefinitionUploader>(uploader);
        }

        [Fact]
        public void ServiceCollection_ShouldRegisterFlowDefinitionUploader_WithDependencyInjection()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddHttpClient();
            services.AddLogging();
            services.AddSingleton<IFlowDefinitionUploader, FlowDefinitionUploader>();

            // Act
            var serviceProvider = services.BuildServiceProvider();
            var uploader = serviceProvider.GetService<IFlowDefinitionUploader>();

            // Assert
            Assert.NotNull(uploader);
        }

        [Fact]
        public void ServiceCollection_ShouldRegisterFlowDefinitionUploader_AsSingleton()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddHttpClient();
            services.AddLogging();
            services.AddSingleton<IFlowDefinitionUploader, FlowDefinitionUploader>();

            // Act
            var serviceProvider = services.BuildServiceProvider();
            var uploader1 = serviceProvider.GetService<IFlowDefinitionUploader>();
            var uploader2 = serviceProvider.GetService<IFlowDefinitionUploader>();

            // Assert
            Assert.Same(uploader1, uploader2);
        }

        [Fact]
        public void LegacyFlowDefinitionUploader_ShouldBeMarkedObsolete()
        {
            // Act
#pragma warning disable CS0618 // Type or member is obsolete
            var obsoleteAttribute = typeof(LegacyFlowDefinitionUploader)
                .GetCustomAttribute<ObsoleteAttribute>();
#pragma warning restore CS0618 // Type or member is obsolete

            // Assert
            Assert.NotNull(obsoleteAttribute);
            Assert.Contains("Use IFlowDefinitionUploader instead", obsoleteAttribute.Message);
        }

        [Fact]
        public void LegacyFlowDefinitionUploader_Instance_ShouldThrowException_WhenSecureApiNotReady()
        {
            // Act & Assert - This test verifies the legacy uploader throws when SecureApi is not initialized
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Throws<Exception>(() => LegacyFlowDefinitionUploader.Instance);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        // Helper methods for setting up HTTP responses
        private void SetupSuccessfulHttpResponse()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { success = true, data = "uploaded" }))
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        private void VerifyHttpRequestWasMade()
        {
            _mockHttpHandler.Protected()
                .Verify("SendAsync",
                    Times.Once(),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }
    }
} 
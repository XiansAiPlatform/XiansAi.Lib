using System;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using Xunit;
using XiansAi.Router.Plugins;

namespace XiansAi.Lib.Tests.UnitTests.Router.Plugins
{
    /*
    dotnet test --filter "FullyQualifiedName~Router.Plugins.CapabilityKnowledgeLoaderTests"
    */
    public class CapabilityKnowledgeLoaderTests
    {
        private readonly string _knowledgePath;

        public CapabilityKnowledgeLoaderTests()
        {
            // Get the output directory for the tests
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _knowledgePath = Path.Combine(assemblyLocation!, "../../../Knowledge");
            
            // Set environment variable to the full path of Knowledge folder
            Environment.SetEnvironmentVariable("LOCAL_INSTRUCTIONS_FOLDER", _knowledgePath);
        }
        
        /*
        dotnet test --filter "FullyQualifiedName~Router.Plugins.CapabilityKnowledgeLoaderTests"
        */
        [Fact]
        public void Load_ShouldReturnCapabilityKnowledgeModel_WhenKnowledgeExists()
        {
            Environment.SetEnvironmentVariable("LOCAL_INSTRUCTIONS_FOLDER", _knowledgePath);
            // Act
            var result = CapabilityKnowledgeLoader.Load("TestCapability");
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test capability description", result.Description);
            Assert.Equal("Returns a test result", result.Returns);
            Assert.Equal(2, result.Parameters.Count);
            Assert.Equal("First parameter description", result.Parameters["param1"]);
            Assert.Equal("Second parameter description", result.Parameters["param2"]);
        }
        
        [Fact]
        public async Task LoadAsync_ShouldReturnCapabilityKnowledgeModel_WhenKnowledgeExists()
        {
            Environment.SetEnvironmentVariable("LOCAL_INSTRUCTIONS_FOLDER", _knowledgePath);
            // Act
            var result = await CapabilityKnowledgeLoader.LoadAsync("TestCapability");
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test capability description", result.Description);
            Assert.Equal("Returns a test result", result.Returns);
            Assert.Equal(2, result.Parameters.Count);
            Assert.Equal("First parameter description", result.Parameters["param1"]);
            Assert.Equal("Second parameter description", result.Parameters["param2"]);
        }
        
        [Fact]
        public void Load_ShouldReturnNull_WhenKnowledgeDoesNotExist()
        {
            Environment.SetEnvironmentVariable("LOCAL_INSTRUCTIONS_FOLDER", _knowledgePath);
            // Act & Assert
            // The exception is wrapped in AggregateException when using Task.Wait()
            var result = CapabilityKnowledgeLoader.Load("NonExistentCapability");
            Assert.Null(result);
        }
        
        [Fact]
        public async Task LoadAsync_ShouldReturnNull_WhenKnowledgeDoesNotExist()
        {
            Environment.SetEnvironmentVariable("LOCAL_INSTRUCTIONS_FOLDER", _knowledgePath);
            // Act & Assert
            var result = await CapabilityKnowledgeLoader.LoadAsync("NonExistentCapability");
            Assert.Null(result);
        }
        
        [Fact]
        public void Load_ShouldThrowInvalidOperationException_WhenKnowledgeFileIsMissingRequiredFields()
        {
            Environment.SetEnvironmentVariable("LOCAL_INSTRUCTIONS_FOLDER", _knowledgePath);
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => CapabilityKnowledgeLoader.Load("InvalidCapability"));
            Assert.Contains("missing a description", exception.Message);
        }
        
        [Fact]
        public async Task LoadAsync_ShouldThrowInvalidOperationException_WhenKnowledgeFileIsMissingRequiredFields()
        {
            Environment.SetEnvironmentVariable("LOCAL_INSTRUCTIONS_FOLDER", _knowledgePath);
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CapabilityKnowledgeLoader.LoadAsync("InvalidCapability"));
            Assert.Contains("missing a description", exception.Message);
        }
    }
} 
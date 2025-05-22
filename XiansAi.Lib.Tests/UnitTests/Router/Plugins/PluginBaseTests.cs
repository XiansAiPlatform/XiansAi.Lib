using XiansAi.Flow.Router.Plugins;

namespace XiansAi.Lib.Tests.UnitTests.Router.Plugins
{
    /*
    dotnet test --filter "FullyQualifiedName~Router.Plugins.PluginBaseTests"
    */
    public class PluginBaseTests
    {
        [Fact]
        public void GetFunctions_ShouldReturnAllCapabilityMethods()
        {
            // Arrange
            var pluginType = typeof(TestPlugin);

            // Act
            var functions = PluginBase<TestPlugin>.GetFunctions().ToList();

            // Assert
            Assert.Equal(2, functions.Count);
            Assert.Contains(functions, f => f.Name == "TestCapability1");
            Assert.Contains(functions, f => f.Name == "TestCapability2");
            Assert.DoesNotContain(functions, f => f.Name == "NotACapability");
        }

        [Fact]
        public void GetFunctions_ShouldSetCorrectDescription()
        {
            // Arrange
            var pluginType = typeof(TestPlugin);

            // Act
            var functions = TestPlugin.GetFunctions().ToList();

            // Assert
            var function = functions.First(f => f.Name == "TestCapability1");
            Assert.Equal("This is a test capability", function.Description);
        }

        [Fact]
        public void GetFunctions_ShouldSetCorrectParameterMetadata()
        {
            // Arrange
            var pluginType = typeof(TestPlugin);

            // Act
            var functions = TestPlugin.GetFunctions().ToList();

            // Assert
            var function = functions.First(f => f.Name == "TestCapability1");
            var parameters = function.Metadata.Parameters.ToList();
            
            Assert.Equal(2, parameters.Count);
            Assert.Equal("testParam", parameters[0].Name);
            Assert.Equal("Test parameter description", parameters[0].Description);
            Assert.Equal("testParam2", parameters[1].Name);
            Assert.Equal("Test parameter 2 description", parameters[1].Description);
        }

        [Fact]
        public void GetFunctions_ShouldThrowException_WhenParameterHasNoDescription()
        {
            // Arrange
            var pluginType = typeof(InvalidPlugin);

            // Act & Assert
            var exception = Assert.Throws<Exception>(() => InvalidPlugin.GetFunctions().ToList());
            Assert.Contains("has no description", exception.Message);
        }

        // Test plugins for the unit tests
        private class TestPlugin: PluginBase<TestPlugin>
        {
            [Capability("This is a test capability")]
            [Parameter("testParam", "Test parameter description")]
            [Parameter("testParam2", "Test parameter 2 description")]
            public static string TestCapability1(
                string testParam,
                string testParam2)
            {
                return "Result";
            }

            [Capability("This is another test capability")]
            public static string TestCapability2()
            {
                return "Result";
            }

            public static string NotACapability()
            {
                return "Result";
            }
        }

        private class InvalidPlugin : PluginBase<InvalidPlugin>
        {
            [Capability("Invalid capability")]
            public static string InvalidCapability(string paramWithNoDescription)
            {
                return "Result";
            }
        }
    }
} 
using System.Reflection;
using XiansAi.Onboarding;
using XiansAi.Flow;
using Xunit;

namespace XiansAi.Lib.Tests.UnitTests.Onboarding;

public class EmbeddedResourceTests
{
    [Fact]
    public void Parse_WithEmbeddedProtocol_LoadsFromAssembly()
    {
        // Arrange - This test uses the test knowledge files already embedded
        var json = @"{
            ""workflow"": [{
                ""value"": ""embedded://Knowledge/TestCapability.json""
            }]
        }";
        
        var assembly = Assembly.GetExecutingAssembly();
        
        // Act
        var result = OnboardingParser.Parse(json, null, assembly);
        
        // Assert
        Assert.NotNull(result);
        // The TestCapability.json should be loaded
        Assert.DoesNotContain("embedded://", result);
    }

    [Fact]
    public void Parse_WithEmbeddedProtocol_WithoutAssembly_LeavesUnchanged()
    {
        // Arrange
        var json = @"{
            ""workflow"": [{
                ""value"": ""embedded://some-resource.md""
            }]
        }";
        
        // Act - No assembly provided, so embedded:// should be left as-is
        var result = OnboardingParser.Parse(json, null, null);
        
        // Assert
        Assert.Contains("embedded://some-resource.md", result);
    }

    [Fact]
    public void Parse_WithMixedProtocols_ResolvesBoth()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        
        var fileContent = "Content from file system";
        var testFile = Path.Combine(testDir, "test.md");
        File.WriteAllText(testFile, fileContent);
        
        try
        {
            var json = $@"{{
                ""workflow"": [
                    {{""value"": ""file://test.md""}},
                    {{""value"": ""embedded://Knowledge/TestCapability.json""}}
                ]
            }}";
            
            var assembly = Assembly.GetExecutingAssembly();
            
            // Act
            var result = OnboardingParser.Parse(json, testDir, assembly);
            
            // Assert
            Assert.Contains(fileContent, result);
            Assert.DoesNotContain("file://", result);
            Assert.DoesNotContain("embedded://", result);
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void RunnerOptions_SetOnboardingJsonWithAssembly_LoadsEmbeddedResources()
    {
        // Arrange
        var json = @"{
            ""display-name"": ""Test"",
            ""version"": ""1.0.0"",
            ""workflow"": [{
                ""value"": ""embedded://Knowledge/TestCapability.json""
            }]
        }";
        
        var assembly = Assembly.GetExecutingAssembly();
        var options = new RunnerOptions();
        
        // Act
        options.SetOnboardingJsonWithAssembly(json, assembly);
        
        // Assert
        Assert.NotNull(options.OnboardingJson);
        Assert.DoesNotContain("embedded://", options.OnboardingJson);
    }

    [Fact]
    public void RunnerOptions_SetOnboardingJsonWithAssembly_WithFileProtocol_LoadsFiles()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        
        var testFile = Path.Combine(testDir, "content.md");
        File.WriteAllText(testFile, "Test content");
        
        try
        {
            // Temporarily change directory to test location
            var originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(testDir);
            
            var json = @"{
                ""workflow"": [{
                    ""value"": ""file://content.md""
                }]
            }";
            
            var options = new RunnerOptions();
            
            // Act
            options.SetOnboardingJsonWithAssembly(json);
            
            // Assert
            Assert.Contains("Test content", options.OnboardingJson);
            
            // Restore directory
            Directory.SetCurrentDirectory(originalDir);
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void Parse_WithInvalidEmbeddedResource_ThrowsFileNotFoundException()
    {
        // Arrange
        var json = @"{
            ""workflow"": [{
                ""value"": ""embedded://NonExistentResource.md""
            }]
        }";
        
        var assembly = Assembly.GetExecutingAssembly();
        
        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() => 
            OnboardingParser.Parse(json, null, assembly)
        );
        
        Assert.Contains("NonExistentResource.md", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void Parse_WithEmbeddedProtocol_HandlesBackslashes()
    {
        // Arrange - Test with backslash path separator (escaped for JSON)
        var json = @"{
            ""workflow"": [{
                ""value"": ""embedded://Knowledge\\TestCapability.json""
            }]
        }";
        
        var assembly = Assembly.GetExecutingAssembly();
        
        // Act
        var result = OnboardingParser.Parse(json, null, assembly);
        
        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("embedded://", result);
    }

    [Fact]
    public void Parse_WithEmbeddedProtocol_HandlesForwardSlashes()
    {
        // Arrange - Test with forward slash path separator
        var json = @"{
            ""workflow"": [{
                ""value"": ""embedded://Knowledge/TestCapability.json""
            }]
        }";
        
        var assembly = Assembly.GetExecutingAssembly();
        
        // Act
        var result = OnboardingParser.Parse(json, null, assembly);
        
        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("embedded://", result);
    }

    [Fact]
    public void SetOnboardingJsonWithAssembly_WithNullJson_SetsNull()
    {
        // Arrange
        var options = new RunnerOptions();
        
        // Act
        options.SetOnboardingJsonWithAssembly(null!);
        
        // Assert
        Assert.Null(options.OnboardingJson);
    }

    [Fact]
    public void SetOnboardingJsonWithAssembly_WithEmptyJson_SetsEmpty()
    {
        // Arrange
        var options = new RunnerOptions();
        
        // Act
        options.SetOnboardingJsonWithAssembly("");
        
        // Assert
        Assert.Equal("", options.OnboardingJson);
    }
}



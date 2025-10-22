using System.Reflection;
using XiansAi.Onboarding;
using Xunit;

namespace XiansAi.Lib.Tests.UnitTests.Onboarding;

public class OnboardingParserTests
{
    [Fact]
    public void Parse_WithNoReferences_ReturnsOriginalJson()
    {
        // Arrange
        var json = @"{
            ""display-name"": ""Test Agent"",
            ""version"": ""1.0.0"",
            ""workflow"": [{
                ""step"": ""knowledge"",
                ""value"": ""This is plain text without file references""
            }]
        }";
        
        // Act
        var result = OnboardingParser.Parse(json);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("This is plain text without file references", result);
        Assert.DoesNotContain("file://", result);
    }

    [Fact]
    public void Parse_WithFileReference_LoadsContent()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        
        var testFile = Path.Combine(testDir, "test-content.md");
        var expectedContent = "# Test Content\n\nThis is test content from a file.";
        File.WriteAllText(testFile, expectedContent);
        
        try
        {
            var json = $@"{{
                ""display-name"": ""Test Agent"",
                ""version"": ""1.0.0"",
                ""workflow"": [{{
                    ""step"": ""knowledge"",
                    ""value"": ""file://test-content.md""
                }}]
            }}";
            
            // Act
            var result = OnboardingParser.Parse(json, testDir);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains(expectedContent, result);
            Assert.DoesNotContain("file://", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void Parse_WithMissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var json = @"{
            ""workflow"": [{
                ""value"": ""file://nonexistent-file.md""
            }]
        }";
        
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => OnboardingParser.Parse(json));
    }

    [Fact]
    public void Parse_WithMultipleFileReferences_LoadsAllFiles()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        
        var file1 = Path.Combine(testDir, "file1.md");
        var file2 = Path.Combine(testDir, "file2.md");
        var content1 = "Content from file 1";
        var content2 = "Content from file 2";
        File.WriteAllText(file1, content1);
        File.WriteAllText(file2, content2);
        
        try
        {
            var json = $@"{{
                ""workflow"": [
                    {{""value"": ""file://file1.md""}},
                    {{""value"": ""file://file2.md""}}
                ]
            }}";
            
            // Act
            var result = OnboardingParser.Parse(json, testDir);
            
            // Assert
            Assert.Contains(content1, result);
            Assert.Contains(content2, result);
            Assert.DoesNotContain("file://", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void Parse_WithNestedFileReferences_LoadsNestedFiles()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var nestedDir = Path.Combine(testDir, "nested", "path");
        Directory.CreateDirectory(nestedDir);
        
        var testFile = Path.Combine(nestedDir, "nested-file.md");
        var expectedContent = "Nested file content";
        File.WriteAllText(testFile, expectedContent);
        
        try
        {
            var json = @"{
                ""workflow"": [{
                    ""value"": ""file://nested/path/nested-file.md""
                }]
            }";
            
            // Act
            var result = OnboardingParser.Parse(json, testDir);
            
            // Assert
            Assert.Contains(expectedContent, result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void Parse_WithInvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidJson = @"{
            ""workflow"": [
                {""value"": ""test""
            ]  // Missing closing brace
        ";
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => OnboardingParser.Parse(invalidJson));
    }

    [Fact]
    public void Parse_WithNullJson_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => OnboardingParser.Parse(null!));
    }

    [Fact]
    public void Parse_WithEmptyJson_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => OnboardingParser.Parse(""));
        Assert.Throws<ArgumentException>(() => OnboardingParser.Parse("   "));
    }

    [Fact]
    public void Parse_WithNestedObjects_ProcessesCorrectly()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        
        var testFile = Path.Combine(testDir, "content.md");
        var expectedContent = "Test content";
        File.WriteAllText(testFile, expectedContent);
        
        try
        {
            var json = @"{
                ""workflow"": [{
                    ""step"": ""knowledge"",
                    ""metadata"": {
                        ""nested"": {
                            ""value"": ""file://content.md""
                        }
                    }
                }]
            }";
            
            // Act
            var result = OnboardingParser.Parse(json, testDir);
            
            // Assert
            Assert.Contains(expectedContent, result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void Validate_WithRequiredFields_ReturnsTrue()
    {
        // Arrange
        var json = @"{
            ""display-name"": ""Test Agent"",
            ""version"": ""1.0.0"",
            ""workflow"": [
                {""step"": ""knowledge""}
            ]
        }";
        
        // Act
        var isValid = OnboardingParser.Validate(json, out var errors);
        
        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithMissingRequiredFields_ReturnsFalse()
    {
        // Arrange
        var json = @"{
            ""display-name"": ""Test Agent""
        }";
        
        // Act
        var isValid = OnboardingParser.Validate(json, out var errors);
        
        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("version"));
        Assert.Contains(errors, e => e.Contains("workflow"));
    }

    [Fact]
    public void Validate_WithEmptyWorkflow_ReturnsFalse()
    {
        // Arrange
        var json = @"{
            ""display-name"": ""Test Agent"",
            ""version"": ""1.0.0"",
            ""workflow"": []
        }";
        
        // Act
        var isValid = OnboardingParser.Validate(json, out var errors);
        
        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("workflow") && e.Contains("empty"));
    }

    [Fact]
    public void Validate_WithInvalidJson_ReturnsFalse()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        
        // Act
        var isValid = OnboardingParser.Validate(invalidJson, out var errors);
        
        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Invalid JSON"));
    }

    [Fact]
    public void Parse_PreservesJsonStructure_AfterLoadingFiles()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        
        var testFile = Path.Combine(testDir, "content.md");
        File.WriteAllText(testFile, "Content");
        
        try
        {
            var json = @"{
                ""display-name"": ""Test"",
                ""version"": ""1.0.0"",
                ""workflow"": [{
                    ""step"": ""knowledge"",
                    ""name"": ""Test Knowledge"",
                    ""value"": ""file://content.md""
                }]
            }";
            
            // Act
            var result = OnboardingParser.Parse(json, testDir);
            
            // Assert
            Assert.Contains("\"display-name\"", result);
            Assert.Contains("\"version\"", result);
            Assert.Contains("\"workflow\"", result);
            Assert.Contains("\"step\"", result);
            Assert.Contains("\"name\"", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void Parse_WithMixedContent_ProcessesOnlyFileReferences()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        
        var testFile = Path.Combine(testDir, "content.md");
        File.WriteAllText(testFile, "File content");
        
        try
        {
            var json = @"{
                ""workflow"": [
                    {""value"": ""Plain text content""},
                    {""value"": ""file://content.md""},
                    {""value"": ""More plain text""}
                ]
            }";
            
            // Act
            var result = OnboardingParser.Parse(json, testDir);
            
            // Assert
            Assert.Contains("Plain text content", result);
            Assert.Contains("File content", result);
            Assert.Contains("More plain text", result);
            // File reference should be replaced
            Assert.DoesNotContain("file://content.md", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    [Fact]
    public void Parse_WithDifferentFileTypes_LoadsAll()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        
        var mdFile = Path.Combine(testDir, "file.md");
        var txtFile = Path.Combine(testDir, "file.txt");
        var jsonFile = Path.Combine(testDir, "file.json");
        
        File.WriteAllText(mdFile, "Markdown content");
        File.WriteAllText(txtFile, "Text content");
        File.WriteAllText(jsonFile, "{\"key\": \"value\"}");
        
        try
        {
            var json = @"{
                ""workflow"": [
                    {""value"": ""file://file.md""},
                    {""value"": ""file://file.txt""},
                    {""value"": ""file://file.json""}
                ]
            }";
            
            // Act
            var result = OnboardingParser.Parse(json, testDir);
            
            // Assert
            Assert.Contains("Markdown content", result);
            Assert.Contains("Text content", result);
            Assert.Contains("{\"key\": \"value\"}", result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }
}



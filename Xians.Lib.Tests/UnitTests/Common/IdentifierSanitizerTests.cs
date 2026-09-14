using System.Text;
using System.Text.Json;
using Xians.Lib.Common;

namespace Xians.Lib.Tests.UnitTests.Common;

/// <summary>
/// Unicode agent-name validation matching the server
/// (<c>^[\p{L}\p{M}\p{N}\s._@|+\-:/\\,#=]+$</c>) plus NFC normalization.
/// </summary>
public class IdentifierSanitizerTests
{
    public const string NorwegianAgentName = "Kjøpsassistent";

    [Theory]
    [InlineData("Kjøpsassistent")]
    [InlineData("Blåbær Agent")]
    [InlineData("Ærlige Ønsker")]
    [InlineData("My Agent")]
    [InlineData("agent_1")]
    [InlineData("user@host")]
    [InlineData("path/name")]
    [InlineData("name+tag")]
    [InlineData("name=value")]
    [InlineData("name,list")]
    [InlineData("name#hash")]
    public void SanitizeAndValidateAgentName_AcceptsUnicodeAndAllowedPunctuation(string name)
    {
        Assert.Equal(name, IdentifierSanitizer.SanitizeAndValidateAgentName(name));
    }

    [Fact]
    public void SanitizeAndValidateAgentName_TrimsWhitespace()
    {
        Assert.Equal(NorwegianAgentName, IdentifierSanitizer.SanitizeAndValidateAgentName($"  {NorwegianAgentName}  "));
    }

    [Fact]
    public void SanitizeAndValidateAgentName_NfcNormalizesDecomposedARing()
    {
        // å (U+00E5) vs a + combining ring above (U+0061 U+030A)
        var composed = "Kåre";
        var decomposed = "Ka\u030Are";

        Assert.NotEqual(composed, decomposed);
        Assert.Equal(composed, decomposed.Normalize(NormalizationForm.FormC));
        Assert.Equal(composed, IdentifierSanitizer.SanitizeAndValidateAgentName(decomposed));
        Assert.Equal(
            IdentifierSanitizer.NormalizeForLookup(composed),
            IdentifierSanitizer.NormalizeForLookup(decomposed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeAndValidateAgentName_RejectsNullOrWhitespace(string? name)
    {
        var ex = Assert.Throws<ArgumentException>(() => IdentifierSanitizer.SanitizeAndValidateAgentName(name));
        Assert.Contains("cannot be null or empty", ex.Message);
    }

    [Theory]
    [InlineData("bad:name")]
    [InlineData("Kjøpsassistent:Chat")]
    public void SanitizeAndValidateAgentName_RejectsColon(string name)
    {
        var ex = Assert.Throws<ArgumentException>(() => IdentifierSanitizer.SanitizeAndValidateAgentName(name));
        Assert.Contains("cannot contain ':'", ex.Message);
    }

    [Theory]
    [InlineData("foo<bar")]
    [InlineData("foo>bar")]
    [InlineData("foo\"bar")]
    [InlineData("foo'bar")]
    [InlineData("foo{bar")]
    [InlineData("foo}bar")]
    public void SanitizeAndValidateAgentName_RejectsMarkupCharacters(string name)
    {
        var ex = Assert.Throws<ArgumentException>(() => IdentifierSanitizer.SanitizeAndValidateAgentName(name));
        Assert.Contains("invalid characters", ex.Message);
    }

    [Fact]
    public void SanitizeAndValidateWorkflowType_AllowsColonAndNorwegianLetters()
    {
        var workflowType = $"{NorwegianAgentName}:Supervisor Workflow";

        Assert.Equal(workflowType, IdentifierSanitizer.SanitizeAndValidateWorkflowType(workflowType));
    }

    [Fact]
    public void SanitizeAndValidateWorkflowName_RejectsColon()
    {
        Assert.Throws<ArgumentException>(() =>
            IdentifierSanitizer.SanitizeAndValidateWorkflowName("Supervisor:Workflow"));
    }

    [Fact]
    public void SanitizeAndValidateActivationName_AcceptsNorwegianLetters()
    {
        Assert.Equal("Kjøp-øst", IdentifierSanitizer.SanitizeAndValidateActivationName("Kjøp-øst"));
    }

    [Fact]
    public void UnicodeJson_PassesNorwegianLettersThroughUnescaped()
    {
        var json = JsonSerializer.Serialize(new { agentName = NorwegianAgentName }, UnicodeJson.SerializerOptions);

        Assert.Contains(NorwegianAgentName, json);
        Assert.DoesNotContain("\\u00f8", json, StringComparison.OrdinalIgnoreCase);
    }
}

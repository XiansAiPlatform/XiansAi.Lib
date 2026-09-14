using System.Text;
using System.Text.Json;
using Xians.Lib.Common;

namespace Xians.Lib.Tests.UnitTests.Common;

/// <summary>
/// Unicode identifier validation: NFC normalization, literal-space allow-list,
/// markup/control-character rejection, and length caps.
/// </summary>
public class IdentifierSanitizerTests
{
    public const string NorwegianAgentName = "Kjøpsassistent";

    private static readonly Func<string?, string>[] AllValidators =
    [
        v => IdentifierSanitizer.SanitizeAndValidateAgentName(v),
        v => IdentifierSanitizer.SanitizeAndValidateActivationName(v),
        v => IdentifierSanitizer.SanitizeAndValidateWorkflowName(v),
        v => IdentifierSanitizer.SanitizeAndValidateWorkflowType(v),
    ];

    private static readonly Func<string?, string>[] NameValidatorsWithoutColon =
    [
        v => IdentifierSanitizer.SanitizeAndValidateAgentName(v),
        v => IdentifierSanitizer.SanitizeAndValidateActivationName(v),
        v => IdentifierSanitizer.SanitizeAndValidateWorkflowName(v),
    ];

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
    [InlineData("name|pipe")]
    [InlineData("name-dash")]
    [InlineData(@"name\path")]
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

    [Fact]
    public void SanitizeAndValidateAgentName_NfcNormalizesCombiningAcute()
    {
        var composed = "café";
        var decomposed = "cafe\u0301";

        Assert.Equal(composed, IdentifierSanitizer.SanitizeAndValidateAgentName(decomposed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AllValidators_RejectNullOrWhitespace(string? name)
    {
        foreach (var validate in AllValidators)
        {
            var ex = Assert.Throws<ArgumentException>(() => validate(name));
            Assert.Contains("cannot be null or empty", ex.Message);
        }
    }

    [Theory]
    [InlineData("foo<bar")]
    [InlineData("foo>bar")]
    [InlineData("foo\"bar")]
    [InlineData("foo'bar")]
    [InlineData("foo{bar")]
    [InlineData("foo}bar")]
    public void AllValidators_RejectMarkupCharacters(string name)
    {
        foreach (var validate in AllValidators)
        {
            var ex = Assert.Throws<ArgumentException>(() => validate(name));
            Assert.Contains("invalid characters", ex.Message);
        }
    }

    [Theory]
    [InlineData("foo\r\nbar")]
    [InlineData("foo\nbar")]
    [InlineData("foo\rbar")]
    [InlineData("foo\tbar")]
    [InlineData("foo\vbar")]
    [InlineData("foo\fbar")]
    [InlineData("foo\u0085bar")]
    [InlineData("foo\u2028bar")]
    [InlineData("foo\u2029bar")]
    public void AllValidators_RejectControlAndLineSeparators(string name)
    {
        foreach (var validate in AllValidators)
        {
            var ex = Assert.Throws<ArgumentException>(() => validate(name));
            Assert.Contains("invalid characters", ex.Message);
        }
    }

    [Theory]
    [InlineData("bad:name")]
    [InlineData("Kjøpsassistent:Chat")]
    public void NameValidators_RejectColon(string name)
    {
        foreach (var validate in NameValidatorsWithoutColon)
        {
            var ex = Assert.Throws<ArgumentException>(() => validate(name));
            Assert.Contains("cannot contain ':'", ex.Message);
        }
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
    public void SanitizeAndValidateAgentName_RejectsOverMaxLength()
    {
        var name = new string('a', IdentifierSanitizer.MaxNameLength + 1);
        var ex = Assert.Throws<ArgumentException>(() => IdentifierSanitizer.SanitizeAndValidateAgentName(name));
        Assert.Contains("maximum length", ex.Message);
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void SanitizeAndValidateAgentName_AcceptsMaxLength()
    {
        var name = new string('a', IdentifierSanitizer.MaxNameLength);
        Assert.Equal(name, IdentifierSanitizer.SanitizeAndValidateAgentName(name));
    }

    [Fact]
    public void SanitizeAndValidateWorkflowType_AcceptsCombinedMaxLength()
    {
        var workflowType = $"{new string('a', IdentifierSanitizer.MaxNameLength)}:{new string('b', IdentifierSanitizer.MaxNameLength)}";
        Assert.Equal(IdentifierSanitizer.MaxWorkflowTypeLength, workflowType.Length);
        Assert.Equal(workflowType, IdentifierSanitizer.SanitizeAndValidateWorkflowType(workflowType));
    }

    [Fact]
    public void SanitizeAndValidateWorkflowType_RejectsOverMaxLength()
    {
        var workflowType = new string('a', IdentifierSanitizer.MaxWorkflowTypeLength + 1);
        var ex = Assert.Throws<ArgumentException>(() => IdentifierSanitizer.SanitizeAndValidateWorkflowType(workflowType));
        Assert.Contains("maximum length", ex.Message);
    }

    [Fact]
    public void UnicodeJson_PassesNorwegianLettersThroughUnescaped()
    {
        var payload = new Dictionary<string, string> { ["agentName"] = NorwegianAgentName };
        var json = JsonSerializer.Serialize(payload, UnicodeJson.SerializerOptions);

        Assert.Contains(NorwegianAgentName, json);
        Assert.DoesNotContain("\\u00f8", json, StringComparison.OrdinalIgnoreCase);

        var roundTrip = JsonSerializer.Deserialize<Dictionary<string, string>>(json, UnicodeJson.SerializerOptions);
        Assert.Equal(NorwegianAgentName, roundTrip!["agentName"]);
    }

    [Fact]
    public void UnicodeJson_EscapesHtmlSensitiveCharacters()
    {
        var json = JsonSerializer.Serialize(new { text = "<script>&" }, UnicodeJson.SerializerOptions);

        Assert.DoesNotContain("<script>", json);
        Assert.DoesNotContain("&", json);
    }
}

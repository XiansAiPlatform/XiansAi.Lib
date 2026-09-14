using System.Text;
using System.Text.RegularExpressions;

namespace Xians.Lib.Common;

/// <summary>
/// Shared Unicode-safe identifier validation matching the Xians server
/// (<c>Agent.SanitizeAndValidateName</c>). Names are NFC-normalized so composed
/// vs decomposed characters (e.g. <c>å</c>) compare equal across lib and server.
/// </summary>
/// <remarks>
/// Allowed characters: Unicode letters (including Norwegian æ, ø, å), combining marks,
/// numbers, a single ASCII space, and <c>._@|+-/\ ,#=</c>. Markup/injection characters
/// (<c>&lt; &gt; " ' { }</c>) and control / line-separator whitespace (TAB, CR, LF, etc.)
/// are rejected. Colon is allowed only in workflow types (<c>Agent:Flow</c>); agent names,
/// activation names, and workflow names still reject <c>:</c> because it is the
/// workflow-identifier delimiter. Tenant IDs stay ASCII.
/// </remarks>
public static class IdentifierSanitizer
{
    /// <summary>
    /// Maximum length of an agent, activation, or workflow name after trim and NFC.
    /// Matches the agent-name cap used by knowledge APIs.
    /// </summary>
    public const int MaxNameLength = 256;

    /// <summary>
    /// Maximum length of a workflow type (<c>Agent:Flow</c>): two names plus the colon delimiter.
    /// </summary>
    public const int MaxWorkflowTypeLength = MaxNameLength * 2 + 1;

    /// <summary>
    /// Unicode-safe pattern used by the server for fields that store or look up agent names
    /// and workflow identifiers. Colon is included so workflow types (<c>Agent:Flow</c>) match.
    /// Whitespace is a literal space only — not the <c>\s</c> class — so TAB/CR/LF and
    /// Unicode line separators cannot enter identifiers.
    /// </summary>
    public const string AllowedPattern = @"^[\p{L}\p{M}\p{N} ._@|+\-:/\\,#=]+$";

    /// <summary>
    /// Same as <see cref="AllowedPattern"/> but without colon, for agent / activation / workflow names.
    /// </summary>
    public const string AllowedPatternWithoutColon = @"^[\p{L}\p{M}\p{N} ._@|+\-/\\,#=]+$";

    private static readonly Regex AllowedRegex = new(
        AllowedPattern,
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AllowedWithoutColonRegex = new(
        AllowedPatternWithoutColon,
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Trims, NFC-normalizes, and validates an agent name. Rejects colon (workflow-ID delimiter)
    /// and markup characters.
    /// </summary>
    public static string SanitizeAndValidateAgentName(string? name, string paramName = "name")
    {
        var sanitized = NormalizeRequired(name, paramName, MaxNameLength);
        ValidateAllowed(sanitized, paramName, allowColon: false, kind: "Agent name");
        return sanitized;
    }

    /// <summary>
    /// Trims, NFC-normalizes, and validates an activation name. Rejects colon because activation
    /// names are embedded in workflow IDs as the id postfix.
    /// </summary>
    public static string SanitizeAndValidateActivationName(string? name, string paramName = "name")
    {
        var sanitized = NormalizeRequired(name, paramName, MaxNameLength);
        ValidateAllowed(sanitized, paramName, allowColon: false, kind: "Activation name");
        return sanitized;
    }

    /// <summary>
    /// Trims, NFC-normalizes, and validates a workflow name (the part after <c>Agent:</c>).
    /// Rejects colon because workflow types use a single colon delimiter.
    /// </summary>
    public static string SanitizeAndValidateWorkflowName(string? name, string paramName = "name")
    {
        var sanitized = NormalizeRequired(name, paramName, MaxNameLength);
        ValidateAllowed(sanitized, paramName, allowColon: false, kind: "Workflow name");
        return sanitized;
    }

    /// <summary>
    /// Trims, NFC-normalizes, and validates a workflow type (<c>Agent:Flow</c>). Colon is allowed.
    /// </summary>
    public static string SanitizeAndValidateWorkflowType(string? workflowType, string paramName = "workflowType")
    {
        var sanitized = NormalizeRequired(workflowType, paramName, MaxWorkflowTypeLength);
        ValidateAllowed(sanitized, paramName, allowColon: true, kind: "Workflow type");
        return sanitized;
    }

    /// <summary>
    /// Trims and NFC-normalizes a value for lookups so composed vs decomposed characters match.
    /// Does not validate character set (invalid lookup keys simply miss).
    /// </summary>
    public static string NormalizeForLookup(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value?.Trim() ?? string.Empty;
        }

        return value.Trim().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Trims and NFC-normalizes a required identifier. Throws when null, whitespace, or too long.
    /// </summary>
    public static string NormalizeRequired(string? value, string paramName, int maxLength = MaxNameLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} cannot be null or empty.", paramName);
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{paramName} exceeds maximum length of {maxLength} characters.",
                paramName);
        }

        return normalized;
    }

    private static void ValidateAllowed(string value, string paramName, bool allowColon, string kind)
    {
        var regex = allowColon ? AllowedRegex : AllowedWithoutColonRegex;
        if (regex.IsMatch(value))
        {
            return;
        }

        if (!allowColon && value.Contains(':'))
        {
            throw new ArgumentException(
                $"{kind} cannot contain ':' character as it is used as a delimiter in workflow identifiers.",
                paramName);
        }

        throw new ArgumentException(
            $"{kind} contains invalid characters. Names may include Unicode letters (including æ, ø, å), " +
            "combining marks, numbers, spaces, and ._@|+-/\\,#=" +
            (allowColon ? ":" : string.Empty) +
            " but not markup characters such as < > \" ' { }.",
            paramName);
    }
}

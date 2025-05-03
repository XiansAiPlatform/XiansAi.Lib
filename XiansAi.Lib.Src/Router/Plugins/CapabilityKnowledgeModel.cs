using System.Text.Json.Serialization;

namespace XiansAi.Router.Plugins;

/// <summary>
/// Represents the structure of the JSON file referenced by CapabilityKnowledgeAttribute.
/// E.g.,
/// {
///   "description": "This is a sample capability that demonstrates the capability knowledge feature",
///   "returns": "Returns a string result with the concatenated inputs",
///   "parameters": {
///     "input1": "The first input parameter to concatenate",
///     "input2": "The second input parameter to concatenate",
///     "input3": "Optional third input parameter to concatenate"
///   }
/// } 
/// </summary>
public class CapabilityKnowledgeModel
{
    /// <summary>
    /// Description of the capability function.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Description of the return value.
    /// </summary>
    [JsonPropertyName("returns")]
    public string Returns { get; set; } = string.Empty;

    /// <summary>
    /// Dictionary of parameter names to their descriptions.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
} 
using XiansAi.Flow;

namespace XiansAi.Lib.Tests.Knowledge;

/// <summary>
/// Example showing how to use the OnboardingJson file reference feature
/// in a real agent application.
/// </summary>
public static class ExampleUsage
{
    // Example 1: Simple onboarding configuration with file references
    public static class Onboarding
    {
        public static string MyAgent = @"
        {
            ""display-name"": ""My Agent"",
            ""version"": ""1.0.0"",
            ""description"": ""An intelligent agent with external knowledge files"",
            ""author"": ""Your Company"",
            ""workflow"": [
                {
                    ""step"": ""knowledge"",
                    ""name"": ""System Prompt"",
                    ""description"": ""Main instructions for the agent"",
                    ""type"": ""markdown"",
                    ""value"": ""file://knowledge-base/system-prompt.md""
                },
                {
                    ""step"": ""knowledge"",
                    ""name"": ""User Guide"",
                    ""description"": ""Instructions for end users"",
                    ""type"": ""markdown"",
                    ""value"": ""file://knowledge-base/user-guide.md""
                },
                {
                    ""step"": ""knowledge"",
                    ""name"": ""Capabilities"",
                    ""description"": ""Available tools and capabilities"",
                    ""type"": ""markdown"",
                    ""value"": ""file://knowledge-base/capabilities.md""
                },
                {
                    ""step"": ""activate"",
                    ""name"": ""Activate the following bots"",
                    ""value"": [""My Agent: Main Workflow""]
                }
            ]
        }";
    }

    // Example 2: How to use in Program.cs
    public static void ExampleProgramMain()
    {
        // Create options with onboarding JSON
        var options = new RunnerOptions 
        {
            SystemScoped = true,
            OnboardingJson = Onboarding.MyAgent  // Files automatically loaded here!
        };

        // The OnboardingJson property setter automatically:
        // 1. Detects file:// references
        // 2. Loads files from disk
        // 3. Replaces references with actual content
        // 4. Returns fully resolved JSON

        // Now options.OnboardingJson contains the complete JSON
        // with all file contents loaded
        
        // Use with AgentTeam
        // var agent = new AgentTeam("MyAgent", options);
        // agent.AddAgent<MyAgentWorkflow>()
        //     .AddAgentCapabilities<MyCapabilities>();
        // await agent.RunAsync();
    }

    // Example 3: Environment-specific configurations
    public static class EnvironmentSpecificOnboarding
    {
        private static string Environment => 
            System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        public static string MyAgent = $@"
        {{
            ""display-name"": ""My Agent ({Environment})"",
            ""version"": ""1.0.0"",
            ""workflow"": [{{
                ""step"": ""knowledge"",
                ""value"": ""file://knowledge-base/{Environment.ToLower()}/system-prompt.md""
            }}]
        }}";
    }

    // Example 4: Multiple file types
    public static class MultiFileTypeOnboarding
    {
        public static string DataAgent = @"
        {
            ""workflow"": [
                {
                    ""name"": ""System Prompt"",
                    ""type"": ""markdown"",
                    ""value"": ""file://prompts/system.md""
                },
                {
                    ""name"": ""Database Schema"",
                    ""type"": ""sql"",
                    ""value"": ""file://schemas/database.sql""
                },
                {
                    ""name"": ""Configuration"",
                    ""type"": ""json"",
                    ""value"": ""file://config/settings.json""
                },
                {
                    ""name"": ""Examples"",
                    ""type"": ""text"",
                    ""value"": ""file://examples/sample-queries.txt""
                }
            ]
        }";
    }

    // Example 5: Bypass parsing for pre-processed JSON
    public static void ExampleBypassParsing()
    {
        var preProcessedJson = @"{
            ""workflow"": [
                {""value"": ""file://should-not-load.md""}
            ]
        }";

        var options = new RunnerOptions();
        
        // Use SetRawOnboardingJson to skip parsing
        options.SetRawOnboardingJson(preProcessedJson);
        
        // Now the file:// reference remains as-is (not loaded)
    }

    // Example 6: Manual parsing with custom base directory
    public static void ExampleManualParsing()
    {
        var onboardingJson = @"{
            ""workflow"": [{
                ""value"": ""file://custom-path/prompt.md""
            }]
        }";

        // Parse manually with custom base directory
        var customBaseDir = "/app/custom-location";
        var parsedJson = XiansAi.Onboarding.OnboardingParser.Parse(
            onboardingJson, 
            customBaseDir
        );

        var options = new RunnerOptions();
        options.SetRawOnboardingJson(parsedJson);
    }

    // Example 7: Validation before use
    public static bool ValidateOnboarding(string onboardingJson)
    {
        var isValid = XiansAi.Onboarding.OnboardingParser.Validate(
            onboardingJson, 
            out var errors
        );

        if (!isValid)
        {
            Console.WriteLine("Onboarding validation failed:");
            foreach (var error in errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }

        return isValid;
    }
}



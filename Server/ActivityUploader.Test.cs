using Xunit;
using XiansAi.Http;

namespace XiansAi.Server;

public class ActivityUploaderTest
{

    private readonly string _testCertPath = "/Users/hasithy/Downloads/xians-ai.pfx";
    private readonly string _testCertPassword = "test";

    public ActivityUploaderTest()
    {
        SecureApi.Initialize(_testCertPath, _testCertPassword, "http://localhost:5257");
    }

    /*
    dotnet test --filter "FullyQualifiedName~ActivityUploaderTest.TestUploadActivity"
    */
    [Fact]
    public async Task TestUploadActivity()
    {
        var activity = new Models.Activity {
            ActivityId = "125",
            ActivityName = "TestActivity",
            StartedTime = DateTime.UtcNow,
            EndedTime = DateTime.UtcNow,
            Inputs = new Dictionary<string, object?> {
                { "stringValue", "test" },
                { "numberValue", 42 },
                { "boolValue", true },
                { "objectValue", new { name = "nested", value = 123 } },
                { "nullValue", null }
            },
            Result = null,
            WorkflowId = "456",
            WorkflowType = "TestWorkflow",
            TaskQueue = "TestQueue",
            AgentName = "TestAgent",
            InstructionIds = new List<string> { "1", "2", "3" }
        };
        
        var uploader = new ActivityUploader();
        await uploader.UploadActivity(activity);
    }
}   
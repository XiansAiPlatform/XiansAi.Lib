using Xunit;
using Temporalio.Activities;
using Temporalio.Workflows;
using XiansAi.Activity;

namespace XiansAi.Flow;

public class Company
{
    public string? Name { get; set; }
    public string? Url { get; set; }
}

[Agents("flowmaxer/search-agent")]
public class CompanyStub: AgentStub
{
    [Activity]
    public async Task<List<Company>> GetCompanies(string link)
    {
        await Task.Delay(1000);
        return new List<Company> { new Company { Name = "Company 1", Url = link } };
    }
}

[Agents("flowmaxer/scraper-agent")]
[Instructions("You are a scraper", "find links")]
public class LinkStub: AgentStub
{
    [Activity("Get Links")]
    public async Task<List<string>> GetLinks(string sourceLink, string prompt)
    {
        Console.WriteLine("Getting links for " + sourceLink + " with prompt " + prompt);
        await Task.Delay(1000);
        return new List<string> { "https://www.google.com", "https://www.bing.com" };
    }
}

[Workflow]
public class MarketingFlow
{

    [WorkflowRun]
    public async Task<List<Company>> RunAsync(string sourceLink, string prompt)
    {
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) };
        var isvCompanies = new List<Company>();

        var links = await Workflow.ExecuteActivityAsync(
            (LinkStub a) => a.GetLinks(sourceLink, prompt), options);

        var companies = await Workflow.ExecuteActivityAsync(
            (CompanyStub a) => a.GetCompanies(links[0]), options);
        
        await Workflow.DelayAsync(TimeSpan.FromSeconds(10));

        return companies;
    }

}

public class FlowMetadataServiceTests
{

    /*
    dotnet test --filter "Name=ExtractFlowInformation_ValidWorkflow_ReturnsCorrectMetadata"

    */
    [Fact]
    public void ExtractFlowInformation_ValidWorkflow_ReturnsCorrectMetadata()
    {
        // Arrange
        var service = new FlowMetadataService<MarketingFlow>();
        
        var activities = new Dictionary<Type, object>
        {
            { typeof(LinkStub), new LinkStub() },
            { typeof(CompanyStub), new CompanyStub() }
        };

        var flow = new FlowInfo<MarketingFlow>();
        flow.AddActivities<LinkStub>(new LinkStub());
        flow.AddActivities<CompanyStub>(new CompanyStub());
        
        // Act
        var flowInfo = service.ExtractFlowInformation(flow);

        // Assert
        Assert.NotNull(flowInfo);
        Assert.Equal("MarketingFlow", flowInfo.TypeName);
        Assert.Equal(typeof(MarketingFlow).FullName, flowInfo.ClassName);
        
        // Verify parameters
        Assert.Equal(2, flowInfo.Parameters.Count);
        Assert.Equal("sourceLink", flowInfo.Parameters[0].Name);
        Assert.Equal("prompt", flowInfo.Parameters[1].Name);
        
        // Verify activities
        Assert.NotEmpty(flowInfo.Activities);

        var activity = flowInfo.Activities.First();
        Assert.Equal("flowmaxer/scraper-agent", activity.Agents[0]);
        Assert.Equal("Get Links", activity.ActivityName);
        Assert.Equal(2, activity.Instructions.Count);
        
        var activity2 = flowInfo.Activities[1];
        Assert.Equal("flowmaxer/search-agent", activity2.Agents[0]);
        Assert.Equal("GetCompanies", activity2.ActivityName);
        Assert.Empty(activity2.Instructions);

    }

}
using Temporalio.Activities;
using Temporalio.Workflows;
using XiansAi.Activity;
using XiansAi.Flow;
using XiansAi.Http;
using XiansAi.Temporal;
using Xunit;

namespace XiansAi.Server.Tests;

public class FlowDefinitionUploaderTest
{

    private readonly string _testCertPath = "../../../../Certificates/XiansAI/xians-ai.pfx";
    private readonly string _testCertPassword = "test";

    public FlowDefinitionUploaderTest()
    {
        SecureApi.Initialize(_testCertPath, _testCertPassword, "http://localhost:5257");
    }

    /*
    dotnet test --filter "FullyQualifiedName~FlowDefinitionUploaderTest.UploadFlowDefinition"
    */
    [Fact]
    public async Task UploadFlowDefinition()
    {
        var flow = new FlowInfo<MarketingFlow>();
        flow.AddActivity<ILinkActivity>(new LinkActivity());
        flow.AddActivity<ICompanyActivity>(new CompanyActivity());
        

        var flowDefinitionUploader = new FlowDefinitionUploader();
        await flowDefinitionUploader.UploadFlowDefinition(flow, getSource());
    }

    private string getSource()
    {
        return @"
            [Workflow]
            public class MarketingFlow
            {

                [WorkflowRun]
                public async Task<List<Company>> RunAsync(string sourceLink, string prompt)
                {
                    var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) };
                    var isvCompanies = new List<Company>();

                    var links = await Workflow.ExecuteActivityAsync(
                        (LinkActivity a) => a.GetLinks(sourceLink, prompt), options);

                    var companies = await Workflow.ExecuteActivityAsync(
                        (CompanyActivity a) => a.GetCompanies(links[0]), options);
                    
                    await Workflow.DelayAsync(TimeSpan.FromSeconds(70));

                    return companies;
                }

            }
        
        ";
    }

}


public class Company
{
    public string? Name { get; set; }
    public string? Url { get; set; }
}


public interface ICompanyActivity
{
    [Activity]
    Task<List<Company>> GetCompanies(string link);
}

[DockerImage("flowmaxer/search-agent")]
public class CompanyActivity: ActivityBase, ICompanyActivity
{
    [Activity]
    public async Task<List<Company>> GetCompanies(string link)
    {
        await Task.Delay(1000);
        return new List<Company> { new Company { Name = "Company 1", Url = link } };
    }
}

public interface ILinkActivity
{
    [Activity]
    Task<List<string>> GetLinks(string sourceLink, string prompt);
}

[DockerImage("flowmaxer/scraper-agent")]
[Instructions("You are a scraper", "find links")]
public class LinkActivity: ActivityBase, ILinkActivity
{
    [Activity]
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
            (LinkActivity a) => a.GetLinks(sourceLink, prompt), options);

        var companies = await Workflow.ExecuteActivityAsync(
            (CompanyActivity a) => a.GetCompanies(links[0]), options);
        
        await Workflow.DelayAsync(TimeSpan.FromSeconds(10));

        return companies;
    }

}
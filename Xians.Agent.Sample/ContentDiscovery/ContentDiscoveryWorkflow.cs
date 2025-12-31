using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Agent.Sample;
using Xians.Lib.Agents.A2A;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling.Models;

[Workflow(Constants.AgentName + ":Content Discovery Workflow")]
public class ContentDiscoveryWorkflow
{

    private readonly ILogger<ContentDiscoveryWorkflow> _logger;

    private int? _intervalMinutes;
    private string? _contentSiteURL;

    public ContentDiscoveryWorkflow()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<ContentDiscoveryWorkflow>();
    }

    [WorkflowRun]
    public async Task<List<string>> RunAsync(string contentSiteURL, int intervalMinutes)
    {
        if (intervalMinutes <= 0)
        {
            throw new ArgumentException("Interval minutes must be greater than 0");
        }

        if (string.IsNullOrEmpty(contentSiteURL) || !Uri.TryCreate(contentSiteURL, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Content site URL is required and must be a valid URL");
        }

        _intervalMinutes = intervalMinutes;
        _contentSiteURL = contentSiteURL;
        _logger.LogInformation("Content site URL: {ContentSiteURL}, Interval minutes: {IntervalMinutes}", contentSiteURL, intervalMinutes);

        // At the start of the workflow, ensure a recurring schedule exists
        await EnsureScheduleExists( intervalMinutes, contentSiteURL );

        _logger.LogInformation("Processing {ContentSiteURL}", contentSiteURL);

        var contentURLs = await FetchContentUrlsAsync(contentSiteURL);

        var newlyProcessedURLs = new List<string>();

        // For each content URL
        foreach (var contentURL in contentURLs)
        {
            // Check if the url is already processed, if not mark it in Document DB as processed
            var isProcessed = await IsContentProcessedAsync(contentURL);
            if (!isProcessed)
            {
                await ProcessContentAsync(contentURL);
                newlyProcessedURLs.Add(contentURL);
            }
        }
        return newlyProcessedURLs;
    }

    private async Task<bool> IsContentProcessedAsync(string contentURL)
    {
        var agent = XiansContext.CurrentAgent;
        var type = "processed-content-url";
        
        // Check if the url is already processed (automatically uses activity when in workflow)
        var doc = await agent.Documents.GetByKeyAsync(type, contentURL);
        
        if (doc != null)
        {
            return true;
        }
        
        // Mark as processed (automatically uses activity when in workflow)
        await agent.Documents.SaveAsync(new Xians.Lib.Agents.Documents.Models.Document
        {
            Type = type,
            Key = contentURL,
            Content = System.Text.Json.JsonSerializer.SerializeToElement(new 
            {
                processedBy = XiansContext.WorkflowId,
                processedAt = DateTime.UtcNow
            })
        });
        
        return false;
    }

    private async Task ProcessContentAsync(string contentURL)
    {
        //TODO: Implement content processing
        _logger.LogInformation("Processing content: {ContentURL}", contentURL);
    }

    /// <summary>
    /// Fetches content URLs from the specified content site using the web agent.
    /// </summary>
    private async Task<List<string>> FetchContentUrlsAsync(string contentSiteURL)
    {
        var webWorkflow = XiansContext.CurrentAgent.GetBuiltInWorkflow(Constants.WebWorkflowName);
        var client = new A2AClient(webWorkflow ?? throw new InvalidOperationException($"{Constants.WebWorkflowName} workflow not found"));
        var response = await client.SendMessageAsync(new A2AMessage 
        { 
            Text = $"Fetch all content article URLs from {contentSiteURL}. Return ONLY the URLs as a comma-separated list with no additional text, explanations, or formatting. Example format: url1,url2,url3" 
        });

        if (string.IsNullOrEmpty(response.Text))
        {
            throw new InvalidOperationException("No response text from web agent");
        }

        var contentURLs = response.Text.Split(',').ToList();
        return contentURLs;
    }

    /// <summary>
    /// Ensures that a recurring schedule exists for this workflow.
    /// Uses the workflow-aware Schedule SDK - automatically uses activities when in workflow context!
    /// </summary>
    private async Task EnsureScheduleExists(int intervalMinutes, string contentSiteURL)
    {
        try
        {
            // Get the current workflow instance using XiansContext
            var workflow = XiansContext.CurrentWorkflow;

            // Call the Schedule SDK directly - it automatically detects workflow context
            // and uses ScheduleActivities under the hood!
            var schedule = await workflow.Schedules!
                .Create($"content-discovery-scheduler-{contentSiteURL}-{intervalMinutes}")
                .WithIntervalSchedule(TimeSpan.FromMinutes(intervalMinutes))
                .WithInput( new object[] { contentSiteURL, intervalMinutes } )
                .StartAsync();

            _logger.LogInformation(
                "Schedule '{ScheduleId}' ensured - will run every {IntervalMinutes} minutes",
                schedule.Id,
                intervalMinutes);
        }
        catch (ScheduleAlreadyExistsException ex)
        {
            _logger.LogInformation(
                "Schedule '{ScheduleId}' already exists, no action needed",
                ex.ScheduleId);
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the workflow
            _logger.LogWarning(
                ex,
                "Failed to create schedule, but continuing workflow execution");
        }
    }
}


using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Agent.Sample;
using Xians.Lib.Agents.A2A;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling.Models;

[Workflow(Constants.AgentName + ":News Discovery Workflow")]
public class NewsDiscoveryWorkflow
{

    private readonly ILogger<NewsDiscoveryWorkflow> _logger;

    private int? _intervalMinutes;
    private string? _newsSiteURL;

    public NewsDiscoveryWorkflow()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<NewsDiscoveryWorkflow>();
    }

    [WorkflowRun]
    public async Task<List<string>> RunAsync(string newsSiteURL, int intervalMinutes)
    {
        if (intervalMinutes <= 0)
        {
            throw new ArgumentException("Interval minutes must be greater than 0");
        }

        if (string.IsNullOrEmpty(newsSiteURL) || !Uri.TryCreate(newsSiteURL, UriKind.Absolute, out _))
        {
            throw new ArgumentException("News site URL is required and must be a valid URL");
        }

        _intervalMinutes = intervalMinutes;
        _newsSiteURL = newsSiteURL;
        _logger.LogInformation("News site URL: {NewsSiteURL}, Interval minutes: {IntervalMinutes}", newsSiteURL, intervalMinutes);

        // At the start of the workflow, ensure a recurring schedule exists
        await EnsureScheduleExists( intervalMinutes, newsSiteURL );

        _logger.LogInformation("Processing {NewsSiteURL}", newsSiteURL);

        var newsURLs = await FetchNewsUrlsAsync(newsSiteURL);

        var newlyProcessedURLs = new List<string>();

        // For each news URL
        foreach (var newsURL in newsURLs)
        {
            // Check if the url is already processed, if not mark it in Document DB as processed
            var isProcessed = await IsNewsProcessedAsync(newsURL);
            if (!isProcessed)
            {
                await ProcessNewsAsync(newsURL);
                newlyProcessedURLs.Add(newsURL);
            }
        }
        return newlyProcessedURLs;
    }

    private async Task<bool> IsNewsProcessedAsync(string newsURL)
    {
        var agent = XiansContext.CurrentAgent;
        var type = "processed-news-url";
        
        // Check if the url is already processed (automatically uses activity when in workflow)
        var doc = await agent.Documents.GetByKeyAsync(type, newsURL);
        
        if (doc != null)
        {
            return true;
        }
        
        // Mark as processed (automatically uses activity when in workflow)
        await agent.Documents.SaveAsync(new Xians.Lib.Agents.Documents.Models.Document
        {
            Type = type,
            Key = newsURL,
            Content = System.Text.Json.JsonSerializer.SerializeToElement(new 
            {
                processedBy = XiansContext.WorkflowId,
                processedAt = DateTime.UtcNow
            })
        });
        
        return false;
    }

    private async Task ProcessNewsAsync(string newsURL)
    {
        //TODO: Implement news processing
        _logger.LogInformation("Processing news: {NewsURL}", newsURL);
    }

    /// <summary>
    /// Fetches news URLs from the specified news site using the web agent.
    /// </summary>
    private async Task<List<string>> FetchNewsUrlsAsync(string newsSiteURL)
    {
        var webWorkflow = XiansContext.CurrentAgent.GetBuiltInWorkflow(Constants.WebWorkflowName);
        var client = new A2AClient(webWorkflow ?? throw new InvalidOperationException($"{Constants.WebWorkflowName} workflow not found"));
        var response = await client.SendMessageAsync(new A2AMessage 
        { 
            Text = $"Fetch all news article URLs from {newsSiteURL}. Return ONLY the URLs as a comma-separated list with no additional text, explanations, or formatting. Example format: url1,url2,url3" 
        });

        if (string.IsNullOrEmpty(response.Text))
        {
            throw new InvalidOperationException("No response text from web agent");
        }

        var newsURLs = response.Text.Split(',').ToList();
        return newsURLs;
    }

    /// <summary>
    /// Ensures that a recurring schedule exists for this workflow.
    /// Uses the workflow-aware Schedule SDK - automatically uses activities when in workflow context!
    /// </summary>
    private async Task EnsureScheduleExists(int intervalMinutes, string newsSiteURL)
    {
        try
        {
            // Get the current workflow instance using XiansContext
            var workflow = XiansContext.CurrentWorkflow;

            // Call the Schedule SDK directly - it automatically detects workflow context
            // and uses ScheduleActivities under the hood!
            var schedule = await workflow.Schedules!
                .Create($"news-discovery-scheduler-{newsSiteURL}-{intervalMinutes}")
                .WithIntervalSchedule(TimeSpan.FromMinutes(intervalMinutes))
                .WithInput( new object[] { newsSiteURL, intervalMinutes } )
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
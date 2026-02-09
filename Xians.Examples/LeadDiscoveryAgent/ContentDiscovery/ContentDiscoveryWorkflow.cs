using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Xians.Agent.Sample;
using Xians.Lib.Agents.A2A;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Agents.Workflows;

[Workflow(Constants.AgentName + ":Content Discovery Workflow")]
public class ContentDiscoveryWorkflow
{

    private readonly ILogger<ContentDiscoveryWorkflow> _logger;

    private int _intervalHours;
    private string _contentSiteURL = string.Empty;
    private string _reportingUserID = string.Empty;
    public ContentDiscoveryWorkflow()
    {
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<ContentDiscoveryWorkflow>();
    }

    [WorkflowRun]
    public async Task<List<string>> RunAsync(string contentSiteURL, int intervalHours, string reportingUserID)
    {
        _intervalHours = ValidateInterval(intervalHours);
        _contentSiteURL = ValidateAndNormalizeUrl(contentSiteURL);
        _reportingUserID = reportingUserID;

        _logger.LogInformation("Content site URL: {ContentSiteURL}, Interval hours: {IntervalHours}", _contentSiteURL, _intervalHours);

        // At the start of the workflow, ensure a recurring schedule exists
        await EnsureScheduleExists();

        // Fetch content URLs from the content site
        //var contentURLs = await FetchContentUrlsAsync(_contentSiteURL);
        var contentURLs = TestData.ContentURLs.Split(',').ToList();

        var newContentURLs = new List<string>();

        // For each content URL
        foreach (var contentURL in contentURLs)
        {
            try
            {
                // Check if the url is already processed, if not mark it in Document DB as processed
                var isProcessed = await IsContentProcessedAsync(contentURL);
                if (!isProcessed)
                {
                    newContentURLs.Add(contentURL);
                    await StartContentProcessingWorkflowAsync(contentURL);
                    _logger.LogInformation("Content URL processing started: {ContentURL}", contentURL);
                }
                else
                {
                    _logger.LogInformation("Content URL previously processed: {ContentURL}", contentURL);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content URL: {ContentURL}", contentURL);
            }
        }
        return newContentURLs; // Successfully processed all content URLs
    }

    private int ValidateInterval(int intervalHours)
    {
        const int hoursInMonth = 744; // Approximately 31 days
        
        if (intervalHours <= 0)
        {
            throw new ApplicationFailureException("Interval hours must be greater than 0");
        }
        
        if (intervalHours > hoursInMonth)
        {
            throw new ApplicationFailureException($"Interval hours must be less than a month ({hoursInMonth} hours)");
        }

        return intervalHours;
    }

    private string ValidateAndNormalizeUrl(string contentSiteURL)
    {
        if (string.IsNullOrEmpty(contentSiteURL) || !Uri.TryCreate(contentSiteURL, UriKind.Absolute, out var uri))
        {
            throw new ApplicationFailureException("Content site URL is required and must be a valid URL: " + contentSiteURL);
        }

        // Remove trailing slashes
        var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        
        return normalized;
    }

    private async Task<bool> IsContentProcessedAsync(string contentURL)
    {
        var type = "processed-content-url-for-user-" + _reportingUserID;
        
        // Check if the url is already processed (automatically uses activity when in workflow)
        var doc = await XiansContext.CurrentAgent.Documents.GetByKeyAsync(type, contentURL);
        
        if (doc != null)
        {
            return true;
        }
        
        // Mark as processed (automatically uses activity when in workflow)
        await XiansContext.CurrentAgent.Documents.SaveAsync(new Xians.Lib.Agents.Documents.Models.Document
        {
            Type = type,
            Key = contentURL,
            Content = System.Text.Json.JsonSerializer.SerializeToElement(new 
            {
                processedBy = XiansContext.WorkflowId,
                reportingUserID = _reportingUserID
            })
        });
        
        return false;
    }

    private async Task StartContentProcessingWorkflowAsync(string contentURL)
    {
        try
        {
            await SubWorkflowService.StartAsync<ContentProcessingWorkflow>([contentURL], null, contentURL, _reportingUserID);
        }
        catch (WorkflowAlreadyStartedException)
        {
            _logger.LogInformation("Content processing workflow already running for: {ContentURL}", contentURL);
        }
    }

    /// <summary>
    /// Fetches content URLs from the specified content site using the web agent.
    /// </summary>
    private async Task<List<string>> FetchContentUrlsAsync(string contentSiteURL)
    {
        if (Environment.GetEnvironmentVariable("USE_TEST_DATA") == "true")
        {
            return TestData.ContentURLs.Split(',').ToList();
        }

        // Send A2A message to web workflow using the simplified API
        var response = await XiansContext.A2A.SendChatToBuiltInAsync(
            Constants.WebWorkflowName,
            new A2AMessage 
            { 
                Data = {},
                Text = $"Fetch all content article URLs from {contentSiteURL}. Return ONLY the URLs as a comma-separated list with no additional text, explanations, or formatting. Example format: url1,url2,url3" 
            });

        if (string.IsNullOrEmpty(response.Text))
        {
            throw new InvalidOperationException("No response text from web agent");
        }
        _logger.LogInformation("Content URLs: {ContentURLs}", response.Text);

        var contentURLs = response.Text.Split(',').ToList();

        //remove duplicates
        contentURLs = contentURLs.Distinct().ToList();

        //remove invalid URLs
        contentURLs = contentURLs.Where(url => Uri.TryCreate(url, UriKind.Absolute, out _)).ToList();

        return contentURLs;
    }

    /// <summary>
    /// Ensures that a recurring schedule exists for this workflow.
    /// Uses the workflow-aware Schedule SDK - automatically uses activities when in workflow context!
    /// </summary>
    private async Task EnsureScheduleExists()
    {
        // CreateIfNotExistsAsync is idempotent - no try-catch needed
        var schedule = await XiansContext.CurrentAgent.Schedules
            .Create<ContentDiscoveryWorkflow>($"content-discovery-scheduler-{_contentSiteURL}-{_intervalHours}")
            .WithIntervalSchedule(TimeSpan.FromHours(_intervalHours))
            .WithInput(new object[] { _contentSiteURL, _intervalHours, _reportingUserID })
            .CreateIfNotExistsAsync();   
    }
}


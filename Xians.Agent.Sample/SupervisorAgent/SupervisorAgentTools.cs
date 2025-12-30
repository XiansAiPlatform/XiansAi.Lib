using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.A2A;

namespace Xians.Agent.Sample.SupervisorAgent;

/// <summary>
/// Tools available to the MAF Agent for enhanced functionality.
/// </summary>
internal static class SupervisorAgentTools
{
    private static readonly ILogger _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.Instance.CreateLogger("SupervisorAgentTools");

    /// <summary>
    /// Conducts research on a company and returns detailed information.
    /// </summary>
    [Description("Research a company and get detailed information about it.")]
    public static async Task<string> ResearchCompany([Description("The company name or website URL to research")] string companyIdentifier)
    {
        _logger.LogInformation("ResearchCompany tool invoked with CompanyIdentifier={CompanyIdentifier}", companyIdentifier);

        try
        {
            // Get the Web workflow from the current agent
            var agent = XiansContext.CurrentAgent;  // Company Research Agent
            var workflow = agent.GetBuiltInWorkflow("Web") ?? throw new InvalidOperationException("Web workflow not found");
            
            _logger.LogDebug("Creating A2A client for Web workflow");
            var client = new A2AClient(workflow);
            
            var requestMessage = $"Research this company and get detailed information about it from proff.no. Company: {companyIdentifier}";
            _logger.LogDebug("Sending A2A message to Web agent: {Message}", requestMessage);
            
            var response = await client.SendMessageAsync(new A2AMessage 
            { 
                Text = requestMessage
            });
            
            _logger.LogInformation("ResearchCompany tool completed successfully for CompanyIdentifier={CompanyIdentifier}, ResponseLength={Length}", 
                companyIdentifier, response.Text?.Length ?? 0);
            
            return response.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ResearchCompany tool failed for CompanyIdentifier={CompanyIdentifier}", companyIdentifier);
            throw;
        }
    }
}


using DotNetEnv;
using Microsoft.Extensions.Logging;
using Xians.Examples.MultiAgentOrchastration.FraudDetection;
using Xians.Examples.MultiAgentOrchastration.InvoiceOrchastrator;
using Xians.Examples.MultiAgentOrchastration.InvoiceProcessor;
using Xians.Examples.MultiAgentOrchastration.Validator;
using Xians.Lib.Agents.Core;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL")
    ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set");
var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY")
    ?? throw new InvalidOperationException("XIANS_API_KEY environment variable is not set");

// Initialize Xians Platform
var xiansPlatform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ServerLogLevel = LogLevel.Information
});

// Set up each agent (registration + workflows)
var invoiceOrchastratorAgent = InvoiceOrchastratorAgent.Setup(xiansPlatform);
var fraudDetectionAgent = FraudDetectionAgent.Setup(xiansPlatform);
var validatorAgent = ValidatorAgent.Setup(xiansPlatform);
var invoiceProcessorAgent = InvoiceProcessorAgent.Setup(xiansPlatform);

// Run all four agents concurrently
await Task.WhenAll(
    invoiceOrchastratorAgent.RunAllAsync(),
    fraudDetectionAgent.RunAllAsync(),
    validatorAgent.RunAllAsync(),
    invoiceProcessorAgent.RunAllAsync()
);

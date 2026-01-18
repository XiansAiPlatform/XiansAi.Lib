# Workflow Parameters

## Overview

Workflow parameters are extracted from the `[WorkflowRun]` method signature and registered with the server. This allows the platform to understand what inputs a workflow expects and provide better documentation and validation.

## Adding Descriptions

Use the standard .NET `[Description]` attribute to add descriptive metadata to your workflow and its parameters:

```csharp
using System.ComponentModel;
using Temporalio.Workflows;

[Description("Processes customer orders from submission to completion")]
[Workflow("MyAgent:ProcessOrder")]
public class ProcessOrderWorkflow
{
    [WorkflowRun]
    public async Task<OrderResult> RunAsync(
        [Description("The unique identifier for the order")]
        string orderId,
        
        [Description("The customer ID associated with this order")]
        string customerId,
        
        [Description("The total amount for the order in USD")]
        decimal amount)
    {
        // Workflow implementation
    }
}
```

The `[Description]` on the class provides a workflow summary, while `[Description]` on parameters describes each input.

## Workflow Definition Structure

When a workflow is registered, the system extracts:

**Workflow-level information:**
- **Summary**: The description from the `[Description]` attribute on the workflow class (optional)

**For each parameter:**
- **Name**: The parameter name (e.g., `orderId`, `customerId`)
- **Type**: The parameter type name (e.g., `String`, `Decimal`)
- **Description**: The description from the `[Description]` attribute (optional)
- **Optional**: Whether the parameter has a default value (true/false)

This information is sent to the server as part of the workflow definition:

```json
{
  "agent": "MyAgent",
  "workflowType": "MyAgent:ProcessOrder",
  "summary": "Processes customer orders from submission to completion",
  "parameterDefinitions": [
    {
      "name": "orderId",
      "type": "String",
      "description": "The unique identifier for the order",
      "optional": false
    },
    {
      "name": "customerId",
      "type": "String",
      "description": "The customer ID associated with this order",
      "optional": false
    },
    {
      "name": "amount",
      "type": "Decimal",
      "description": "The total amount for the order in USD",
      "optional": false
    }
  ]
}
```

## Description Attribute

The `[Description]` attribute is a standard .NET attribute from the `System.ComponentModel` namespace. It's widely used across the .NET ecosystem for providing human-readable descriptions of code elements.

### Syntax

```csharp
[Description(description)]
```

### Parameters

- **description** (string): A clear description of what the parameter represents and how it's used

### Usage Example

```csharp
using System.ComponentModel;

[WorkflowRun]
public async Task RunAsync(
    [Description("User ID to process")]
    string userId)
{ }
```

## Best Practices

1. **Be descriptive**: Provide clear, concise descriptions that explain the parameter's purpose
2. **Include units**: For numeric values, specify units (e.g., "Amount in USD", "Timeout in seconds")
3. **Mention constraints**: Note any validation rules or constraints (e.g., "Must be a valid email address")
4. **Use complete sentences**: Write descriptions as complete sentences for better readability

## Examples

## Optional Parameters

Parameters with default values are automatically marked as optional:

```csharp
using System.ComponentModel;
using Temporalio.Workflows;

[Workflow("MyAgent:ProcessData")]
public class ProcessDataWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(
        [Description("Required input data")]
        string data,
        
        [Description("Maximum retry attempts")]
        int maxRetries = 3,
        
        [Description("Timeout in seconds")]
        int timeout = 300)
    {
        // maxRetries and timeout are marked as optional: true
        // data is marked as optional: false
    }
}
```

### Good Descriptions

```csharp
[Description("The email address where notifications will be sent")]
string notificationEmail

[Description("Maximum number of retry attempts (1-10)")]
int maxRetries = 3

[Description("Processing timeout in seconds")]
int timeoutSeconds = 300

[Description("Customer ID in UUID format")]
string customerId
```

### Descriptions to Avoid

```csharp
[Description("email")]  // Too brief
string email

[Description("retries")]  // Not descriptive enough
int retries

[Description("timeout")]  // Missing units
int timeout
```

## XiansAi.Lib.Src (Alternative Library)

The same functionality is available in the `XiansAi.Lib.Src` project with identical usage:

```csharp
using System.ComponentModel;
using Temporalio.Workflows;

[Workflow("MyAgent:ProcessData")]
public class ProcessDataWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(
        [Description("Input data to process")]
        string inputData)
    {
        // Implementation
    }
}
```

## See Also

- [Custom Workflows](A2A-CustomWorkflows.md)
- [Workflow Definition](technical/WorkflowDefinitions.md)
- [Getting Started](GettingStarted.md)

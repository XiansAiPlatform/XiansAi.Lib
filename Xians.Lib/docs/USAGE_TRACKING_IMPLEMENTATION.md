# Usage Tracking Implementation Summary

## Overview

This document summarizes the implementation of usage tracking in **Xians.Lib**, porting the functionality from commit `709f5b3518aa3d512859899fe475a8446a8c0f0e` in **XiansAi.Lib.Src**.

## Key Difference: Framework vs Built-in

### XiansAi.Lib.Src (Old)
- Has **built-in SemanticRouter** with chat completion agents
- Makes LLM calls internally using Semantic Kernel
- Can automatically track token usage from its own LLM calls
- Usage tracking is embedded in `SemanticRouterImpl.cs`

### Xians.Lib (New)
- Is a **framework** where developers implement handlers
- Developers make their own LLM calls (OpenAI, Claude, Azure, etc.)
- Cannot automatically track usage since it doesn't control LLM calls
- Usage tracking provided as a **utility service**

## Implementation Approach

Instead of embedding usage tracking in a specific component (like SemanticRouter), we provide:

1. **UsageEventsClient** - Utility service for reporting usage
2. **Extension methods** - Easy-to-use helpers on `UserMessageContext`
3. **UsageTracker** - Automatic timing helper
4. **Documentation** - Clear examples for different scenarios

## Files Created

### 1. Core Utility
**File**: `Xians.Lib/Common/Usage/UsageEventsClient.cs`

**Purpose**: Singleton client for reporting usage events to the platform server.

**Key Features**:
- `ReportAsync()` - Send usage data to server
- `ExtractUsageFromSemanticKernelResponses()` - Extract tokens from SK responses
- Helper methods for reflection-based token extraction
- Automatic context from `XiansContext`

**Equivalent to**: `XiansAi.Lib.Src/Server/UsageEventsClient.cs`

### 2. Extension Methods
**File**: `Xians.Lib/Common/Usage/UsageTrackingExtensions.cs`

**Purpose**: Make usage tracking easy with extension methods and helper classes.

**Key Features**:
- `context.ReportUsageAsync()` - Extension method on UserMessageContext
- `UsageTracker` - Automatic timing with using/dispose pattern
- Automatic context extraction (tenant, user, workflow, etc.)

**New functionality** - Not in original commit, added for developer convenience.

### 3. Documentation
**File**: `Xians.Lib/docs/UsageTracking.md`

**Purpose**: Comprehensive guide for developers.

**Covers**:
- Quick start examples
- Different LLM provider integrations (OpenAI, Azure, Claude)
- Best practices
- Advanced scenarios
- Troubleshooting

### 4. Working Example
**File**: `Xians.Lib/docs/Examples/UsageTrackingExample.cs`

**Purpose**: Runnable code examples showing different usage patterns.

**Examples**:
- Basic tracking
- Automatic timing
- Multiple LLM calls
- Custom metadata
- Error handling

## Mapping to Original Commit Changes

### Original: XiansAi.Lib.Src/Server/UsageEventsClient.cs (NEW FILE)

**Ported to**: `Xians.Lib/Common/Usage/UsageEventsClient.cs`

**Changes made**:
- ✅ Kept singleton pattern
- ✅ Kept `ReportAsync()` method
- ✅ Kept `ExtractUsageFromResponses()` method (renamed for SK)
- ✅ Kept reflection-based token extraction helpers
- ✅ Kept `UsageEventRecord` model
- ✅ Uses `XiansContext` instead of `AgentContext`
- ✅ Uses agent's `HttpService` instead of `SecureApi`

### Original: XiansAi.Lib.Src/Flow/SemanticRouter/SemanticRouterImpl.cs (MODIFIED)

**Not directly ported** because Xians.Lib doesn't have SemanticRouter.

**Original changes**:
1. Added `UsageEventsClient` field
2. Added response time tracking with `Stopwatch`
3. Added history message count tracking
4. Added `RecordUsageEvents()` private method
5. Called usage tracking in `Completion()` and `Route()` methods

**Equivalent in Xians.Lib**:
- Developers add usage tracking in their **message handlers**
- Use `UsageTracker` for automatic timing
- Use extension methods for easy reporting
- Can track history count manually if needed

## Usage Comparison

### In XiansAi.Lib.Src (Automatic)

```csharp
// Usage tracking happens automatically inside SemanticRouter
var response = await semanticRouter.RouteAsync(messageThread, options);
// ↑ Internally tracks: tokens, timing, history, metadata
```

### In Xians.Lib (Manual)

```csharp
// Developer explicitly tracks usage after LLM call
workflow.OnUserChatMessage(async (context) => 
{
    using var tracker = new UsageTracker(context, "gpt-4");
    
    var response = await OpenAIClient.GetChatCompletionAsync(prompt);
    
    await tracker.ReportAsync(
        response.Usage.PromptTokens,
        response.Usage.CompletionTokens
    );
    
    await context.ReplyAsync(response.Content);
});
```

## Benefits of This Approach

### 1. Flexibility
- Works with **any LLM provider** (OpenAI, Azure, Anthropic, custom APIs)
- Not tied to Semantic Kernel (though SK is supported)
- Developers control when and what to track

### 2. Transparency
- Clear and explicit - developers see exactly what's being tracked
- No hidden/magical tracking behavior
- Easy to add custom metadata

### 3. Consistency
- Same `UsageEventRecord` model as XiansAi.Lib.Src
- Same server endpoint (`/api/agent/usage/report`)
- Same data format

### 4. Developer-Friendly
- Simple extension methods
- Automatic timing with `UsageTracker`
- Comprehensive documentation
- Working examples

## Server-Side Compatibility

The implementation is **100% compatible** with the existing server endpoint:

**Endpoint**: `POST /api/agent/usage/report`

**Payload**: Same `UsageEventRecord` structure:
```json
{
  "tenantId": "tenant123",
  "userId": "user456",
  "model": "gpt-4",
  "promptTokens": 150,
  "completionTokens": 75,
  "totalTokens": 225,
  "messageCount": 1,
  "workflowId": "tenant123:MyAgent:BuiltIn Workflow:user456",
  "requestId": "req-uuid",
  "source": "MyAgent.ChatHandler",
  "metadata": { ... },
  "responseTimeMs": 1234
}
```

## Migration Path

For agents moving from **XiansAi.Lib.Src** to **Xians.Lib**:

### Before (XiansAi.Lib.Src with SemanticRouter)
```csharp
// Usage tracking was automatic
var response = await semanticRouter.RouteAsync(...);
```

### After (Xians.Lib)
```csharp
workflow.OnUserChatMessage(async (context) => 
{
    // Make your LLM call
    var response = await YourLLMCall(context.Message.Text);
    
    // Add explicit tracking
    await context.ReportUsageAsync(
        model: response.Model,
        promptTokens: response.PromptTokens,
        completionTokens: response.CompletionTokens,
        totalTokens: response.TotalTokens
    );
    
    await context.ReplyAsync(response.Text);
});
```

## Testing

To test the implementation:

1. **Run the example**: `UsageTrackingExample.cs`
2. **Send a message** through XiansUI
3. **Verify** the usage data appears in:
   - MongoDB `usageEvents` collection
   - Platform usage dashboard
   - Server logs

## Future Enhancements

Potential improvements:

1. **Automatic extraction helpers** for more LLM providers
2. **Built-in cost calculation** based on model pricing
3. **Usage quotas** and rate limiting integration
4. **Batch reporting** for performance optimization
5. **Usage analytics** helpers (trends, aggregations)

## Questions?

See:
- **Documentation**: `docs/UsageTracking.md`
- **Examples**: `docs/Examples/UsageTrackingExample.cs`
- **Source**: `Common/Usage/UsageEventsClient.cs`
- **Original commit**: `709f5b3518aa3d512859899fe475a8446a8c0f0e` in XiansAi.Lib.Src



# Example Knowledge Base File

This is an example knowledge base file that demonstrates how the OnboardingJson file reference feature works.

## Purpose

This file can be referenced in onboarding JSON using the `file://` protocol:

```json
{
    "workflow": [{
        "step": "knowledge",
        "name": "Example Knowledge",
        "type": "markdown",
        "value": "file://Knowledge/ExampleOnboarding.md"
    }]
}
```

## Content

You are an intelligent AI assistant designed to help users with various tasks.

### Your Capabilities

1. **Data Analysis** - Analyze datasets and identify patterns
2. **Report Generation** - Create comprehensive reports
3. **Insights** - Provide actionable recommendations

### Guidelines

- Always verify the quality of input data
- Explain your reasoning clearly
- Provide examples when helpful
- Be thorough but concise

### Example Interaction

**User**: "Can you analyze this sales data?"

**Assistant**: "I'll help you analyze the sales data. First, let me verify the data quality..."

## Benefits of External Files

By keeping this content in a separate markdown file:

✅ **Easy to Edit** - No escaped JSON strings  
✅ **Version Control** - Clear diffs in git  
✅ **Reusable** - Can be referenced by multiple agents  
✅ **Readable** - Proper markdown formatting  
✅ **Maintainable** - Simple to update and review



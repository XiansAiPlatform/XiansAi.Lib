# Environment Variables Configuration

This document lists all environment variables supported by XiansAi.Lib.

## Authentication & Server

### `APP_SERVER_API_KEY`
- **Required**: Yes (for server operations)
- **Description**: Base64-encoded X509 certificate for API authentication with the XiansAi platform server
- **Format**: The certificate should contain tenant ID (O=) and user ID (OU=) fields
- **Example**: `MIIDXTCCAkWgAwIBAgIJAK...`

### `APP_SERVER_URL`
- **Required**: Yes (for server operations)
- **Description**: Base URL for the XiansAi API server
- **Default**: None
- **Example**: `https://api.xians.ai`

## Temporal Workflow

### `TEMPORAL_SERVER_URL`
- **Required**: Yes (when using Temporal workflows)
- **Description**: URL for connecting to the Temporal workflow engine
- **Default**: None
- **Example**: `localhost:7233`

## Knowledge Management

### `LOCAL_KNOWLEDGE_FOLDER`
- **Required**: No
- **Description**: Path to local folder containing knowledge/instruction files. When set, the system will load knowledge from local files instead of the server
- **Use Case**: Development and testing
- **Default**: None (uses server)
- **Example**: `/path/to/local/knowledge/folder`

### `KNOWLEDGE_CACHE_TTL_MINUTES`
- **Required**: No
- **Description**: How long (in minutes) to cache fetched knowledge before re-fetching. Uses ASP.NET Core's `IMemoryCache` for robust, production-ready caching with automatic memory pressure handling.
- **Default**: `5` (5 minutes)
- **Valid Values**: Positive integers
- **Example**: `5` (for 5 minutes), `60` (for 1 hour), `1440` (for 24 hours)

## Logging

### `CONSOLE_LOG_LEVEL`
- **Required**: No
- **Description**: Controls the verbosity of logs written to console
- **Default**: `INFORMATION`
- **Valid Values**: `TRACE`, `DEBUG`, `INFORMATION`, `WARNING`, `ERROR`, `CRITICAL`
- **Example**: `INFORMATION`

### `API_LOG_LEVEL`
- **Required**: No
- **Description**: Controls the verbosity of logs sent to the API logging service
- **Default**: `INFORMATION`
- **Valid Values**: `TRACE`, `DEBUG`, `INFORMATION`, `WARNING`, `ERROR`, `CRITICAL`
- **Example**: `INFORMATION`

## LLM Configuration

### `LLM_PROVIDER`
- **Required**: No (depends on semantic router usage)
- **Description**: The provider for language model services
- **Valid Values**: Provider-specific (e.g., `OpenAI`, `Azure`, etc.)
- **Example**: `OpenAI`

### `LLM_API_KEY`
- **Required**: No (depends on LLM provider)
- **Description**: API key for authenticating with the LLM provider
- **Example**: `sk-...`

### `LLM_ENDPOINT`
- **Required**: No
- **Description**: Custom endpoint URL for the LLM service (if using a custom deployment)
- **Default**: Provider-specific default
- **Example**: `https://api.openai.com/v1`

### `LLM_DEPLOYMENT_NAME`
- **Required**: No (required for Azure OpenAI)
- **Description**: Deployment name for Azure OpenAI and similar services
- **Example**: `gpt-4-deployment`

### `LLM_MODEL_NAME`
- **Required**: No
- **Description**: Specific model name to use
- **Example**: `gpt-4`, `gpt-3.5-turbo`

## Resource Management

### `UPLOAD_RESOURCES`
- **Required**: No
- **Description**: Whether to automatically upload resources to the server
- **Default**: `false`
- **Valid Values**: `true`, `false`
- **Example**: `false`

## Development & Testing

### `ASPNETCORE_ENVIRONMENT`
- **Required**: No
- **Description**: Determines the runtime environment
- **Default**: `Production`
- **Valid Values**: `Development`, `Staging`, `Production`
- **Example**: `Production`

---

## Example Configuration

Create a `.env` file in your project root with your specific values:

```bash
# Authentication
APP_SERVER_API_KEY=MIIDXTCCAkWgAwIBAgIJAK...
APP_SERVER_URL=https://api.xians.ai

# Temporal
TEMPORAL_SERVER_URL=localhost:7233

# Knowledge
KNOWLEDGE_CACHE_TTL_MINUTES=5

# Logging
CONSOLE_LOG_LEVEL=INFORMATION
API_LOG_LEVEL=WARNING

# LLM
LLM_PROVIDER=OpenAI
LLM_API_KEY=sk-...
LLM_MODEL_NAME=gpt-4
```


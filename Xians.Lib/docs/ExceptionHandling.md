# Exception Handling Guidelines

This document outlines best practices for exception handling in the Xians.Lib library.

## Table of Contents

- [Principles](#principles)
- [Custom Exception Hierarchy](#custom-exception-hierarchy)
- [Exception Handling Patterns](#exception-handling-patterns)
- [Context-Specific Guidelines](#context-specific-guidelines)
- [Logging Best Practices](#logging-best-practices)
- [Anti-Patterns to Avoid](#anti-patterns-to-avoid)
- [Examples](#examples)

---

## Principles

### 1. **Use Specific Exception Types**
- Prefer custom, domain-specific exceptions over generic `Exception`, `InvalidOperationException`, or `ArgumentException`
- Custom exceptions provide better context and enable targeted error handling
- Use the exception hierarchy provided in `Xians.Lib.Common.Exceptions`

### 2. **Fail Fast**
- Validate inputs and configuration early
- Throw exceptions as soon as an error is detected
- Don't let invalid state propagate through the system

### 3. **Provide Context**
- Include relevant information in exception messages (IDs, names, expected vs actual values)
- Use structured exception properties for programmatic access to error details
- Preserve the original exception as `InnerException` when re-throwing

### 4. **Log Appropriately**
- Log exceptions at the appropriate level (Error, Warning, Debug)
- Include structured logging properties for better searchability
- Avoid logging sensitive information (passwords, API keys, PII)

### 5. **Security First**
- Use generic error messages in public-facing exceptions to avoid information disclosure
- Log detailed error information internally for debugging
- Never expose stack traces or internal paths to external callers

### 6. **Handle Temporal Constraints**
- In Temporal workflows, only catch exceptions you can truly handle
- Re-throw exceptions in activities to enable Temporal's retry mechanism
- Use top-level exception handlers in workflow loops to prevent crashes

---

## Custom Exception Hierarchy

### Base Exception
```csharp
XiansException : Exception
```
All custom exceptions inherit from `XiansException`, providing a common base for library-specific errors.

### Domain-Specific Exceptions

#### ConfigurationException
**When to use:** Configuration is missing, invalid, or malformed.

```csharp
throw new ConfigurationException("ServerUrl is required", nameof(ServerUrl));
```

**Properties:**
- `ConfigurationKey` - The configuration key that failed validation

#### CertificateException
**When to use:** Certificate operations fail (parsing, validation, or authentication).

```csharp
throw new CertificateException("Failed to load certificate", innerException);
```

#### TenantIsolationException
**When to use:** A tenant isolation violation is detected.

```csharp
throw new TenantIsolationException(
    "Tenant mismatch detected",
    expectedTenantId: "tenant-a",
    actualTenantId: "tenant-b");
```

**Properties:**
- `ExpectedTenantId` - The expected tenant ID
- `ActualTenantId` - The actual tenant ID received

#### TemporalConnectionException
**When to use:** Temporal connection operations fail.

```csharp
throw new TemporalConnectionException(
    "Failed to connect to Temporal server",
    serverUrl: "localhost:7233",
    @namespace: "default",
    innerException);
```

**Properties:**
- `ServerUrl` - The Temporal server URL
- `Namespace` - The Temporal namespace

#### WorkflowException
**When to use:** Workflow operations fail.

```csharp
throw new WorkflowException(
    "Invalid workflow ID format",
    workflowType: "MyWorkflow",
    workflowId: "invalid-id");
```

**Properties:**
- `WorkflowType` - The workflow type identifier
- `WorkflowId` - The workflow instance ID

---

## Exception Handling Patterns

### Pattern 1: Validation and Early Return

Use for input validation and configuration checks:

```csharp
public void ProcessRequest(string tenantId, string workflowId)
{
    if (string.IsNullOrWhiteSpace(tenantId))
        throw new ArgumentNullException(nameof(tenantId));
    
    if (string.IsNullOrWhiteSpace(workflowId))
        throw new WorkflowException("WorkflowId cannot be empty", null, workflowId);
    
    // Continue processing...
}
```

### Pattern 2: Catch-Wrap-Throw

Use when adding context to an exception:

```csharp
try
{
    var cert = LoadCertificate(apiKey);
}
catch (FormatException ex)
{
    _logger.LogError(ex, "Failed to decode certificate");
    throw new CertificateException("Invalid base64 encoded certificate", ex);
}
```

### Pattern 3: Catch-Log-Rethrow

Use in activity methods to enable Temporal retry:

```csharp
[Activity]
public async Task ProcessMessageAsync(Request request)
{
    try
    {
        await HandleMessageAsync(request);
    }
    catch (Exception ex)
    {
        ActivityExecutionContext.Current.Logger.LogError(ex,
            "Error processing message: RequestId={RequestId}",
            request.RequestId);
        
        throw; // Re-throw to let Temporal handle retry
    }
}
```

### Pattern 4: Selective Exception Catching

Use when you want to handle only specific exception types:

```csharp
try
{
    await ConnectAsync();
}
catch (CertificateException)
{
    // Re-throw certificate exceptions without wrapping
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error during connection");
    throw new TemporalConnectionException("Connection failed", serverUrl, ns, ex);
}
```

### Pattern 5: Retry with Transient Exception Filtering

Use for network operations with automatic retry:

```csharp
catch (Exception ex) when (IsTransientException(ex) && attempt < maxRetries)
{
    lastException = ex;
    _logger.LogWarning(ex,
        "Operation failed on attempt {Attempt}/{MaxAttempts}",
        attempt, maxRetries);
    // Continue loop for retry
}
```

### Pattern 6: Top-Level Exception Handler

Use in workflow loops to prevent crashes:

```csharp
try
{
    await ProcessMessageAsync(message);
}
catch (Exception ex)
{
    // Top-level handler - safe to catch here
    Workflow.Logger.LogError(ex, 
        "Error processing message: {ErrorMessage}",
        ex.Message);
    
    // Attempt recovery (e.g., send error response)
    try
    {
        await SendErrorResponseAsync(message, ex.Message);
    }
    catch (Exception errorEx)
    {
        Workflow.Logger.LogError(errorEx,
            "Failed to send error response");
        // Don't rethrow - already in error state
    }
}
```

---

## Context-Specific Guidelines

### Temporal Workflows

**DO:**
- Use top-level exception handlers in workflow loops to prevent crashes
- Log exceptions with context (RequestId, ParticipantId, WorkflowType)
- Attempt graceful degradation (e.g., send error response to user)

**DON'T:**
- Catch and swallow exceptions without logging
- Use `catch (Exception)` in deterministic workflow code unless at the top level
- Retry operations inside workflow code (use activities instead)

### Temporal Activities

**DO:**
- Catch exceptions to log context
- Re-throw exceptions to enable Temporal's retry mechanism
- Use structured logging with activity context

**DON'T:**
- Catch and swallow exceptions (prevents retry)
- Return error codes instead of throwing (breaks Temporal semantics)

```csharp
// CORRECT
[Activity]
public async Task SendMessageAsync(SendMessageRequest request)
{
    try
    {
        var response = await _httpClient.SendAsync(httpRequest);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Failed to send message. Status: {response.StatusCode}, Error: {error}");
        }
    }
    catch (Exception ex)
    {
        ActivityExecutionContext.Current.Logger.LogError(ex,
            "Error sending message: ParticipantId={ParticipantId}",
            request.ParticipantId);
        throw; // Re-throw for retry
    }
}
```

### HTTP Client Operations

**DO:**
- Distinguish between transient and permanent failures
- Use retry logic for transient exceptions
- Include HTTP status codes in exception messages

**DON'T:**
- Retry on permanent failures (4xx errors except 408, 429)
- Log sensitive headers or request bodies

```csharp
private static bool IsTransientException(Exception ex)
{
    return ex switch
    {
        HttpRequestException httpEx => IsTransientHttpException(httpEx),
        TaskCanceledException => true,
        SocketException => true,
        TimeoutException => true,
        _ => false
    };
}
```

### Configuration Validation

**DO:**
- Validate configuration in `Validate()` methods
- Throw `ConfigurationException` with the configuration key
- Check all constraints (required, format, range)

```csharp
public void Validate()
{
    if (string.IsNullOrWhiteSpace(ServerUrl))
        throw new ConfigurationException("ServerUrl is required", nameof(ServerUrl));
    
    if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
        throw new ConfigurationException(
            $"ServerUrl '{ServerUrl}' is not a valid URL",
            nameof(ServerUrl));
}
```

### Certificate Operations

**DO:**
- Use `CertificateException` for all certificate-related errors
- Use generic error messages for security (avoid information disclosure)
- Log detailed errors internally

**DON'T:**
- Expose certificate details in exception messages
- Log certificate contents or private keys

```csharp
try
{
    var certificateBytes = Convert.FromBase64String(base64Cert);
    var certificate = new X509Certificate2(certificateBytes);
}
catch (FormatException ex)
{
    _logger.LogError(ex, "Failed to decode certificate");
    throw new CertificateException("Invalid base64 encoded certificate", ex);
}
```

### Resource Cleanup

**DO:**
- Use `try-finally` or `using` statements for resource cleanup
- Handle exceptions during disposal gracefully
- Log disposal errors as warnings (not errors)

```csharp
protected virtual void Dispose(bool disposing)
{
    if (_disposed) 
        return;

    if (disposing)
    {
        try
        {
            _client?.Dispose();
            _semaphore?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disposal");
        }
    }

    _disposed = true;
}
```

---

## Logging Best Practices

### Log Levels

- **Error**: Unexpected exceptions that prevent operation completion
- **Warning**: Recoverable errors, retry attempts, health check failures
- **Information**: Successful operations after retry, major state changes
- **Debug**: Detailed flow information, parameter values

### Structured Logging

Use structured logging properties for better searchability:

```csharp
_logger.LogError(ex,
    "Failed to process message: RequestId={RequestId}, WorkflowType={WorkflowType}, Tenant={TenantId}",
    requestId,
    workflowType,
    tenantId);
```

### Security Considerations

**DO NOT log:**
- API keys or certificates
- Passwords or tokens
- Personally Identifiable Information (PII)
- Full stack traces in external logs

**DO log:**
- Request IDs for correlation
- Tenant IDs (for multi-tenant debugging)
- Workflow IDs and types
- HTTP status codes (not response bodies)

---

## Anti-Patterns to Avoid

### ❌ Empty Catch Blocks

```csharp
// BAD
try
{
    await riskyOperation();
}
catch (Exception)
{
    // Silent failure - don't do this
}
```

### ❌ Catching Without Re-throwing

```csharp
// BAD
catch (Exception ex)
{
    _logger.LogError(ex, "Operation failed");
    // Not re-throwing loses the exception
}
```

### ❌ Generic Exception Messages

```csharp
// BAD
throw new Exception("An error occurred");

// GOOD
throw new WorkflowException(
    $"Invalid WorkflowId format. Expected 'TenantId:WorkflowType', got '{workflowId}'",
    workflowType,
    workflowId);
```

### ❌ Using Exception for Control Flow

```csharp
// BAD
try
{
    var value = dictionary[key];
}
catch (KeyNotFoundException)
{
    value = defaultValue;
}

// GOOD
if (!dictionary.TryGetValue(key, out var value))
{
    value = defaultValue;
}
```

### ❌ Losing Inner Exception

```csharp
// BAD
catch (Exception ex)
{
    throw new CustomException("Operation failed");
}

// GOOD
catch (Exception ex)
{
    throw new CustomException("Operation failed", ex);
}
```

### ❌ Over-Catching in Temporal Activities

```csharp
// BAD - Prevents retry
[Activity]
public async Task ProcessAsync(Request request)
{
    try
    {
        await HandleAsync(request);
    }
    catch (Exception)
    {
        return; // Swallowed - Temporal won't retry
    }
}

// GOOD - Allows retry
[Activity]
public async Task ProcessAsync(Request request)
{
    try
    {
        await HandleAsync(request);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Processing failed: {RequestId}", request.Id);
        throw; // Re-throw for Temporal retry
    }
}
```

---

## Examples

### Example 1: Configuration Validation

```csharp
public class ServerConfiguration
{
    public string ServerUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
            throw new ConfigurationException("ServerUrl is required", nameof(ServerUrl));
        
        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
            throw new ConfigurationException(
                $"ServerUrl '{ServerUrl}' is not a valid URL",
                nameof(ServerUrl));
        
        if (TimeoutSeconds <= 0)
            throw new ConfigurationException(
                "TimeoutSeconds must be positive",
                nameof(TimeoutSeconds));
    }
}
```

### Example 2: Retry with Backoff

```csharp
public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
{
    var attempt = 0;
    Exception? lastException = null;

    while (attempt < _maxRetries)
    {
        attempt++;
        
        try
        {
            if (attempt > 1)
            {
                var delay = TimeSpan.FromMilliseconds(
                    _baseDelay * Math.Pow(2, attempt - 2));
                _logger.LogDebug(
                    "Retrying operation (attempt {Attempt}/{MaxAttempts}) after {Delay}ms",
                    attempt, _maxRetries, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }

            return await operation();
        }
        catch (Exception ex) when (IsTransientException(ex) && attempt < _maxRetries)
        {
            lastException = ex;
            _logger.LogWarning(ex,
                "Operation failed on attempt {Attempt}/{MaxAttempts}: {Message}",
                attempt, _maxRetries, ex.Message);
        }
    }

    _logger.LogError(lastException,
        "Operation failed after {MaxAttempts} attempts",
        _maxRetries);
    throw lastException ?? new InvalidOperationException(
        "Operation failed after all retry attempts");
}
```

### Example 3: Temporal Workflow Exception Handling

```csharp
[Workflow]
public class MyWorkflow
{
    private async Task ProcessMessagesLoopAsync()
    {
        while (true)
        {
            await Workflow.WaitConditionAsync(() => _messageQueue.Count > 0);

            if (_messageQueue.TryDequeue(out var message))
            {
                _ = Workflow.RunTaskAsync(async () =>
                {
                    try
                    {
                        await ProcessMessageAsync(message);
                    }
                    catch (Exception ex)
                    {
                        // Top-level handler - safe to catch
                        Workflow.Logger.LogError(ex,
                            "Error processing message: RequestId={RequestId}",
                            message.RequestId);
                        
                        // Attempt recovery
                        try
                        {
                            await SendErrorResponseAsync(message, ex.Message);
                        }
                        catch (Exception errorEx)
                        {
                            Workflow.Logger.LogError(errorEx,
                                "Failed to send error response");
                        }
                    }
                });
            }
        }
    }
}
```

### Example 4: Tenant Isolation Validation

```csharp
public static bool ValidateTenantIsolation(
    string workflowTenantId,
    string? expectedTenantId,
    bool systemScoped,
    ILogger? logger = null)
{
    if (systemScoped)
    {
        // System-scoped agents can handle multiple tenants
        logger?.LogDebug(
            "System-scoped workflow - no tenant validation required. Tenant={Tenant}",
            workflowTenantId);
        return true;
    }
    
    // Non-system-scoped agents must validate tenant isolation
    if (expectedTenantId != workflowTenantId)
    {
        logger?.LogError(
            "Tenant isolation violation: Expected={Expected}, Actual={Actual}",
            expectedTenantId,
            workflowTenantId);
        return false;
    }

    logger?.LogDebug("Tenant validation passed: TenantId={TenantId}", workflowTenantId);
    return true;
}
```

### Example 5: Certificate Loading with Security

```csharp
private CertificateInfo ParseCertificate(string base64EncodedCertificate)
{
    try
    {
        var certificateBytes = Convert.FromBase64String(base64EncodedCertificate);
        var certificate = new X509Certificate2(certificateBytes);

        var tenantId = ExtractTenantIdFromCertificate(certificate);
        var userId = ExtractUserIdFromCertificate(certificate);

        // Generic error messages for security
        if (tenantId == null)
        {
            _logger.LogError("Failed to extract tenant ID from certificate");
            throw new CertificateException(
                "Certificate validation failed: missing required attributes");
        }

        if (userId == null)
        {
            _logger.LogError("Failed to extract user ID from certificate");
            throw new CertificateException(
                "Certificate validation failed: missing required attributes");
        }

        return new CertificateInfo
        {
            Certificate = certificate,
            TenantId = tenantId,
            UserId = userId
        };
    }
    catch (FormatException ex)
    {
        _logger.LogError(ex, "Failed to decode base64 certificate");
        throw new CertificateException("Invalid base64 encoded certificate", ex);
    }
    catch (CertificateException)
    {
        throw; // Re-throw without wrapping
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to read certificate");
        throw new CertificateException("Failed to load certificate", ex);
    }
}
```

---

## Summary

Following these exception handling guidelines ensures:
- **Consistency**: Predictable error handling across the library
- **Debuggability**: Rich context for troubleshooting
- **Security**: No information leakage in error messages
- **Reliability**: Proper retry and recovery mechanisms
- **Maintainability**: Clear patterns for developers to follow

Always consider the context (Temporal workflow vs activity, public API vs internal), use appropriate exception types, log with structure, and fail fast with clear messages.


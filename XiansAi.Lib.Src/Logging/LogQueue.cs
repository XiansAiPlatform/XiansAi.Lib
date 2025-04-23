using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Timers;
using XiansAi.Models;
using Server;

namespace XiansAi.Logging;

public class LogQueue : IDisposable
{
    private readonly ConcurrentQueue<Log> _logQueue = new();
    private readonly System.Timers.Timer _flushTimer;
    private readonly ISecureApiClient _secureApi;
    private readonly string _logApiUrl;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly int _batchSize;
    private readonly int _flushIntervalSeconds;
    private bool _isDisposed = false;
    
    // Event to notify when logs are successfully sent
    public event EventHandler<LogBatchEventArgs>? LogBatchSent;
    
    // Event to notify when log sending fails
    public event EventHandler<LogErrorEventArgs>? LogSendError;

    public LogQueue(ISecureApiClient secureApi, string logApiUrl, int batchSize = 10, int flushIntervalSeconds = 30)
    {
        _secureApi = secureApi ?? throw new ArgumentNullException(nameof(secureApi));
        _logApiUrl = PlatformConfig.APP_SERVER_URL + logApiUrl;
        _batchSize = batchSize;
        _flushIntervalSeconds = flushIntervalSeconds;
        
        // Setup timer for periodic flushing
        _flushTimer = new System.Timers.Timer(_flushIntervalSeconds * 1000);
        _flushTimer.Elapsed += OnFlushTimerElapsed;
        _flushTimer.AutoReset = true;
        _flushTimer.Start();
    }

    public void EnqueueLog(Log log)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(LogQueue));
        
        _logQueue.Enqueue(log);
        
        // If we've reached the batch size, trigger a flush
        if (_logQueue.Count >= _batchSize)
        {
            _ = FlushAsync();
        }
    }

    private void OnFlushTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_logQueue.Count > 0)
        {
            _ = FlushAsync();
        }
    }

    public async Task FlushAsync()
    {
        if (_isDisposed) return;
        
        // Prevent multiple concurrent flushes
        if (!await _flushLock.WaitAsync(0))
        {
            return; // Another flush is in progress
        }
        
        try
        {
            List<Log> batchToSend = new();
            
            // Dequeue up to batchSize logs
            while (batchToSend.Count < _batchSize && _logQueue.TryDequeue(out var log))
            {
                batchToSend.Add(log);
            }
            
            if (batchToSend.Count == 0) return;
            
            await SendLogBatchAsync(batchToSend);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task SendLogBatchAsync(List<Log> logs)
    {
        if (!_secureApi.IsReady)
        {
            Console.Error.WriteLine("App server secure API is not available, log upload failed");
            RequeueLogBatch(logs); // Re-queue the logs
            LogSendError?.Invoke(this, new LogErrorEventArgs("App server secure API is not available", logs));
            return;
        }

        try
        {
            var client = _secureApi.Client;
            var response = await client.PostAsync(_logApiUrl, JsonContent.Create(logs));

            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Logger API failed with status {response.StatusCode}");
                // Re-queue the logs if the API call fails
                RequeueLogBatch(logs);
                LogSendError?.Invoke(this, new LogErrorEventArgs($"Logger API failed with status {response.StatusCode}", logs));
            }
            else
            {
                Console.WriteLine($"Logger API succeeded: {response.StatusCode}, sent {logs.Count} logs");
                LogBatchSent?.Invoke(this, new LogBatchEventArgs(logs.Count));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Logger exception: {ex.Message}");
            // Re-queue the logs if there's an exception
            RequeueLogBatch(logs);
            LogSendError?.Invoke(this, new LogErrorEventArgs(ex.Message, logs));
        }
    }
    
    // Helper method to re-queue a batch of logs
    private void RequeueLogBatch(List<Log> logs)
    {
        foreach (var log in logs)
        {
            _logQueue.Enqueue(log);
        }
    }

    // Ensures all logs are sent before shutdown
    public async Task FlushAllAsync(int timeoutSeconds = 60)
    {
        if (_isDisposed) return;
        
        _flushTimer.Stop();
        
        // Continue flushing until queue is empty or timeout
        var timeoutTask = Task.Delay(timeoutSeconds * 1000);
        
        while (_logQueue.Count > 0)
        {
            var flushTask = FlushAsync();
            var completedTask = await Task.WhenAny(flushTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Console.Error.WriteLine($"Timed out after {timeoutSeconds} seconds while flushing log queue. {_logQueue.Count} logs remain unsent.");
                break;
            }
            
            // Small delay to prevent tight loop
            await Task.Delay(100);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        _flushTimer.Stop();
        _flushTimer.Dispose();
        _flushLock.Dispose();
        
        // Synchronously flush remaining logs
        FlushAllAsync().GetAwaiter().GetResult();
    }
}

public class LogBatchEventArgs : EventArgs
{
    public int LogCount { get; }
    
    public LogBatchEventArgs(int logCount)
    {
        LogCount = logCount;
    }
}

public class LogErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public List<Log> FailedLogs { get; }
    
    public LogErrorEventArgs(string errorMessage, List<Log> failedLogs)
    {
        ErrorMessage = errorMessage;
        FailedLogs = failedLogs;
    }
} 
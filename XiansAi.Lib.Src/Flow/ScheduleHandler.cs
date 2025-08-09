using System.Reflection;
using Temporalio.Workflows;
using XiansAi.Logging;

public class ScheduleHandler : SafeHandler
{
    private readonly Logger<ScheduleHandler> _logger = Logger<ScheduleHandler>.For();

    public ScheduleHandler()
    {
    }

    public async Task InitSchedule()
    {
        _logger.LogDebug("Initializing schedule");
        
        // Get schedule processor configuration
        var scheduleInformation = await Workflow.ExecuteActivityAsync(
            (SystemActivities a) => a.GetScheduleSettings(),
            new SystemActivityOptions());

        Type? scheduleProcessorType = GetProcessorType(scheduleInformation);
        
        // Get all scheduled methods from the processor type
        var scheduledMethods = GetScheduledMethods(scheduleProcessorType);
        
        if (!scheduledMethods.Any())
        {
            _logger.LogWarning("No scheduled methods found in processor type");
            return;
        }

        // Start non-blocking tasks for each scheduled method
        var scheduleTasks = new List<Task>();
        
        foreach (var method in scheduledMethods)
        {
            var task = ExecuteScheduledMethodAsync(scheduleProcessorType, scheduleInformation.ShouldProcessScheduleInWorkflow, method);
            scheduleTasks.Add(task);
        }

        // Wait for all schedule tasks (this won't block the workflow as each task manages its own timing)
        await Task.WhenAll(scheduleTasks);
    }


    private Type GetProcessorType(ScheduleSettings scheduleInformation)
    {
        _logger.LogDebug($"Process data information starting data processing: {scheduleInformation.ScheduleProcessorTypeName}");

        if (scheduleInformation.ScheduleProcessorTypeName == null)
        {
            throw new Exception("Schedule processor type is not set for this flow. Use `flow.SetScheduleProcessor<ScheduleProcessor>(bool)` to set the schedule processor type.");
        }
        var scheduleProcessorType = Type.GetType(scheduleInformation.ScheduleProcessorTypeName)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == scheduleInformation.ScheduleProcessorTypeName);

        if (scheduleProcessorType == null)
        {
            throw new Exception($"Schedule processor type {scheduleInformation.ScheduleProcessorTypeName} not found");
        }

        return scheduleProcessorType;
    }

    private List<MethodInfo> GetScheduledMethods(Type scheduleProcessorType)
    {
        var methods = scheduleProcessorType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes<ScheduleBaseAttribute>().Any())
            .ToList();

        _logger.LogDebug($"Found {methods.Count} scheduled methods in {scheduleProcessorType.Name}");
        
        foreach (var method in methods)
        {
            var attributes = method.GetCustomAttributes<ScheduleBaseAttribute>().ToList();
            _logger.LogDebug($"Method {method.Name} has {attributes.Count} schedule attributes");
        }

        return methods;
    }

    private async Task ExecuteScheduledMethodAsync(Type processorType, bool processInWorkflow, MethodInfo method)
    {
        _logger.LogDebug($"Starting scheduled execution for method: {method.Name}");
        
        
        while (true)
        {
            try
            {
                // Calculate the next execution delay based on the method's schedule attributes
                var delay = GetDelayFromMethodAttribute(method);
                
                _logger.LogDebug($"Next execution for {method.Name} in {delay.TotalMinutes:F2} minutes");
                
                // Wait for the scheduled time (non-blocking in workflow)
                await Workflow.DelayAsync(delay);

                if (processInWorkflow)
                {
                    InvokeScheduledMethod(processorType, method);
                }
                else
                {
                    if (processorType.AssemblyQualifiedName == null)
                    {
                        throw new InvalidOperationException($"Processor type {processorType.Name} has no assembly qualified name");
                    }
                    await Workflow.ExecuteLocalActivityAsync(
                        (SystemActivities a) => a.InvokeScheduledMethod(processorType.AssemblyQualifiedName, method.Name),
                        new SystemLocalActivityOptions());
                }
                
                // To manage the workflow history, we need to check if the workflow should continue as new after each execution
                ContinueAsNew();
            }
            catch (ContinueAsNewException)
            {
                _logger.LogDebug($"ScheduleHandler for {method.Name} is continuing as new");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in scheduled method {method.Name}: {ex.Message}");
                // Continue the loop instead of throwing to maintain scheduling
                continue;
            }
        }
    }

    public static string InvokeScheduledMethod(string processorTypeName, string methodName)
    {
        var processorType = Type.GetType(processorTypeName)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.AssemblyQualifiedName == processorTypeName);
        if (processorType == null)
        {
            throw new InvalidOperationException($"Processor type {processorTypeName} not found");
        }
        var method = processorType.GetMethod(methodName);
        if (method == null)
        {
            throw new InvalidOperationException($"Method {methodName} not found in {processorTypeName}");
        }
        
        return InvokeScheduledMethod(processorType, method);
    }

    public static string InvokeScheduledMethod(Type processorType, MethodInfo method)
    {
        object? processor = null;
        try
        {
            // Create an instance of the processor
            processor = Activator.CreateInstance(processorType);
            
            if (processor == null)
            {
                throw new InvalidOperationException($"Failed to create instance of {processorType.Name}");
            }

            // Invoke the method
            method.Invoke(processor, []);
            
            var result = $"Successfully executed {method.Name} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            
            return result;
        }
        catch (Exception ex)
        {
            var error = $"Error invoking {method.Name}: {ex.Message}";
            throw new InvalidOperationException(error, ex);
        }
        finally
        {
            // Properly dispose of resources if the processor implements IDisposable
            if (processor is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception disposeEx)
                {
                    // Log disposal errors but don't throw them to avoid masking the original exception
                    // Note: Can't use _logger here as this is a static method
                    System.Diagnostics.Debug.WriteLine($"Error disposing processor {processorType.Name}: {disposeEx.Message}");
                }
            }
        }
    }

    // Utility method to get delay based on method attributes
    public static TimeSpan GetDelayFromMethodAttribute(MethodInfo method)
    {
        if (method == null) return TimeSpan.FromHours(1);

        // Check for WeeklyScheduleAttribute
        var weeklyAttr = method.GetCustomAttribute<WeeklyScheduleAttribute>();
        if (weeklyAttr != null)
        {
            return GetNextWeeklyDelay(DateTime.UtcNow, weeklyAttr.DayOfWeek, TimeOnly.Parse(weeklyAttr.Time));
        }

        // Check for DailyScheduleAttribute
        var dailyAttr = method.GetCustomAttribute<DailyScheduleAttribute>();
        if (dailyAttr != null)
        {
            return GetNextDailyDelay(DateTime.UtcNow, TimeOnly.Parse(dailyAttr.Time));
        }

        // Check for MonthlyScheduleAttribute
        var monthlyAttr = method.GetCustomAttribute<MonthlyScheduleAttribute>();
        if (monthlyAttr != null)
        {
            return GetNextMonthlyDelay(DateTime.UtcNow, monthlyAttr.DayOfMonth, TimeOnly.Parse(monthlyAttr.Time));
        }

        // Check for IntervalScheduleAttribute
        var intervalAttr = method.GetCustomAttribute<IntervalScheduleAttribute>();
        if (intervalAttr != null)
        {
            return intervalAttr.Interval;
        }

        return TimeSpan.FromHours(1); // Default
    }

    // Method to get next execution time based on schedule type
    public static TimeSpan GetNextDelay(ScheduleType scheduleType, object? scheduleValue = null)
    {
        var now = DateTime.UtcNow;
        
        return scheduleType switch
        {
            ScheduleType.FixedInterval => scheduleValue is TimeSpan ts ? ts : TimeSpan.FromHours(1),
            
            ScheduleType.Daily => GetNextDailyDelay(now, (TimeOnly?)scheduleValue ?? TimeOnly.Parse("09:00")),
            
            ScheduleType.Weekly => GetNextWeeklyDelay(now, scheduleValue is DayOfWeek dow ? dow : DayOfWeek.Monday, TimeOnly.Parse("14:00")),
            
            ScheduleType.Monthly => GetNextMonthlyDelay(now, scheduleValue is int day ? day : 1, TimeOnly.Parse("09:00")),
            
            ScheduleType.BusinessDays => GetNextBusinessDayDelay(now, TimeOnly.Parse("09:00")),
            
            _ => TimeSpan.FromHours(1) // Default fallback
        };
    }

    private static TimeSpan GetNextDailyDelay(DateTime now, TimeOnly targetTime)
    {
        var today = DateOnly.FromDateTime(now);
        var targetDateTime = today.ToDateTime(targetTime);
        
        if (now >= targetDateTime)
        {
            targetDateTime = today.AddDays(1).ToDateTime(targetTime);
        }
        
        return targetDateTime - now;
    }

    private static TimeSpan GetNextWeeklyDelay(DateTime now, DayOfWeek targetDay, TimeOnly targetTime)
    {
        var daysUntilTarget = (int)targetDay - (int)now.DayOfWeek;
        if (daysUntilTarget <= 0)
            daysUntilTarget += 7;

        var targetDate = DateOnly.FromDateTime(now.AddDays(daysUntilTarget));
        var targetDateTime = targetDate.ToDateTime(targetTime);
        
        // If it's the target day but past the time, move to next week
        if (now.DayOfWeek == targetDay && now.TimeOfDay >= targetTime.ToTimeSpan())
        {
            targetDateTime = targetDateTime.AddDays(7);
        }
        
        return targetDateTime - now;
    }

    private static TimeSpan GetNextMonthlyDelay(DateTime now, int targetDay, TimeOnly targetTime)
    {
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var targetDate = currentMonth.AddDays(Math.Min(targetDay - 1, DateTime.DaysInMonth(now.Year, now.Month) - 1));
        var targetDateTime = DateOnly.FromDateTime(targetDate).ToDateTime(targetTime);
        
        if (now >= targetDateTime)
        {
            var nextMonth = currentMonth.AddMonths(1);
            targetDate = nextMonth.AddDays(Math.Min(targetDay - 1, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month) - 1));
            targetDateTime = DateOnly.FromDateTime(targetDate).ToDateTime(targetTime);
        }
        
        return targetDateTime - now;
    }

    private static TimeSpan GetNextBusinessDayDelay(DateTime now, TimeOnly targetTime)
    {
        var nextBusinessDay = now.AddDays(1);
        
        while (nextBusinessDay.DayOfWeek == DayOfWeek.Saturday || nextBusinessDay.DayOfWeek == DayOfWeek.Sunday)
        {
            nextBusinessDay = nextBusinessDay.AddDays(1);
        }
        
        var targetDateTime = DateOnly.FromDateTime(nextBusinessDay).ToDateTime(targetTime);
        return targetDateTime - now;
    }
}

public enum ScheduleType
{
    FixedInterval,  // Every X minutes/hours/days
    Daily,          // Every day at specific time
    Weekly,         // Every week on specific day and time
    Monthly,        // Every month on specific day
    BusinessDays    // Every business day (Mon-Fri)
}

public class ScheduleBaseAttribute : Attribute
{
}

// Custom scheduling attributes
[AttributeUsage(AttributeTargets.Method)]
public class ScheduleAttribute : ScheduleBaseAttribute
{
    public ScheduleType Type { get; set; }
    public object? Value { get; set; }
    public string? Time { get; set; }
    
    public ScheduleAttribute(ScheduleType type)
    {
        Type = type;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class IntervalScheduleAttribute : ScheduleBaseAttribute
{
    public TimeSpan Interval { get; }
    
    public IntervalScheduleAttribute(int hours = 0, int minutes = 0, int seconds = 0, int days = 0)
    {
        Interval = new TimeSpan(days, hours, minutes, seconds);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class WeeklyScheduleAttribute : ScheduleBaseAttribute
{
    public DayOfWeek DayOfWeek { get; }
    public string Time { get; }
    
    public WeeklyScheduleAttribute(DayOfWeek dayOfWeek, string time = "09:00")
    {
        DayOfWeek = dayOfWeek;
        Time = time;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class DailyScheduleAttribute : ScheduleBaseAttribute
{
    public string Time { get; }
    
    public DailyScheduleAttribute(string time = "09:00") 
    {
        Time = time;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class MonthlyScheduleAttribute : ScheduleBaseAttribute
{
    public int DayOfMonth { get; }
    public string Time { get; }
    
    public MonthlyScheduleAttribute(int dayOfMonth, string time = "09:00")
    {
        DayOfMonth = dayOfMonth;
        Time = time;
    }
}

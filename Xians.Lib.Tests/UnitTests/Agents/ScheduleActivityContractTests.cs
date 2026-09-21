using System.Reflection;
using Temporalio.Activities;
using Temporalio.Client.Schedules;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Agents.Scheduling.Models;
using Xians.Lib.Temporal.Workflows.Scheduling;
using Xians.Lib.Temporal.Workflows.Scheduling.Models;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Contract tests for the schedule activity boundary.
///
/// dotnet test --filter "FullyQualifiedName~ScheduleActivityContract"
/// </summary>
public class ScheduleActivityContractTests
{
    private static readonly IPayloadConverter Converter = DataConverter.Default.PayloadConverter;

    /// <summary>
    /// Every type crossing the activity boundary must be reconstructable by Temporal's JSON
    /// converter. Types without a usable constructor (such as Temporal's own
    /// <see cref="ScheduleDescription"/>) fail only at runtime, on the workflow side, after the
    /// activity has already succeeded - so assert it up front for all schedule activities.
    /// </summary>
    [Fact]
    public void ScheduleActivityPayloads_AreDeserializable()
    {
        var activities = typeof(ScheduleActivities)
            .GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(ActivityAttribute), false).Any())
            .ToList();

        Assert.NotEmpty(activities);

        foreach (var activity in activities)
        {
            foreach (var parameter in activity.GetParameters())
            {
                AssertDeserializable(parameter.ParameterType, $"{activity.Name} parameter '{parameter.Name}'");
            }

            var resultType = UnwrapTask(activity.ReturnType);
            if (resultType != null)
            {
                AssertDeserializable(resultType, $"{activity.Name} return value");
            }
        }
    }

    [Fact]
    public void ScheduleSnapshot_RoundTrips()
    {
        var snapshot = new ScheduleSnapshot
        {
            Id = "tenant:agent:daily",
            Paused = true,
            Note = "paused for maintenance",
            NumActions = 7,
            NumActionsMissedCatchupWindow = 1,
            NumActionsSkippedOverlap = 2,
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            LastUpdatedAt = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            NextActionTimes = [new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc)]
        };

        var result = Converter.ToValue<ScheduleSnapshot>(Converter.ToPayload(snapshot));

        Assert.Equal(snapshot.Id, result.Id);
        Assert.True(result.Paused);
        Assert.Equal(snapshot.Note, result.Note);
        Assert.Equal(snapshot.NumActions, result.NumActions);
        Assert.Equal(snapshot.CreatedAt, result.CreatedAt);
        Assert.Equal(snapshot.LastUpdatedAt, result.LastUpdatedAt);
        Assert.Equal(snapshot.NextActionTimes, result.NextActionTimes);
    }

    /// <summary>
    /// The type this replaced, kept as a live record of why the snapshot exists.
    /// </summary>
    [Fact]
    public void ScheduleDescription_IsNotDeserializable()
    {
        Assert.Empty(typeof(ScheduleDescription).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Throws<NotSupportedException>(() =>
            Converter.ToValue<ScheduleDescription>(Converter.ToPayload(new Dictionary<string, object>())));
    }

    /// <summary>
    /// A schedule that is missing must surface as <see cref="ScheduleNotFoundException"/> in workflow
    /// code, not as a raw activity failure, and must not burn the retry budget getting there.
    /// </summary>
    [Fact]
    public void ScheduleNotFoundException_SurvivesTheActivityBoundary()
    {
        var original = new ScheduleNotFoundException("tenant:agent:daily");

        Assert.True(original is FailureException, "must fail the workflow run, not the workflow task");
        Assert.True(original.NonRetryable, "a missing schedule does not become present by retrying");

        var failure = DataConverter.Default.FailureConverter.ToFailure(original, Converter);
        var rehydrated = DataConverter.Default.FailureConverter.ToException(failure, Converter);

        var applicationFailure = Assert.IsType<ApplicationFailureException>(rehydrated);
        var translated = ScheduleNotFoundException.FromFailure(applicationFailure);

        Assert.NotNull(translated);
        Assert.Equal("tenant:agent:daily", translated!.ScheduleId);
    }

    [Fact]
    public void FromFailure_IgnoresUnrelatedFailures()
    {
        var unrelated = new ApplicationFailureException("nope", errorType: "SomethingElse");

        Assert.Null(ScheduleNotFoundException.FromFailure(unrelated));
    }

    /// <summary>
    /// Null and empty idPostfix address different schedules, which is why the identity returned by
    /// schedule creation must preserve null rather than collapsing it to "".
    /// </summary>
    [Fact]
    public void BuildFullScheduleId_DistinguishesNullFromEmptyIdPostfix()
    {
        var withNull = ScheduleIdHelper.BuildFullScheduleId("tenant", "agent", null, "daily");
        var withEmpty = ScheduleIdHelper.BuildFullScheduleId("tenant", "agent", "", "daily");

        Assert.Equal("tenant:agent:daily", withNull);
        Assert.Equal("tenant:agent::daily", withEmpty);
        Assert.NotEqual(withNull, withEmpty);
    }

    private static void AssertDeserializable(Type type, string description)
    {
        try
        {
            Converter.ToValue(Converter.ToPayload(new Dictionary<string, object>()), type);
        }
        catch (NotSupportedException ex)
        {
            Assert.Fail($"{description} uses '{type.Name}', which Temporal cannot deserialize: {ex.Message}");
        }
        catch
        {
            // Anything else (e.g. a missing 'required' member for this empty payload) still means the
            // type is constructible, which is all this test cares about.
        }
    }

    private static Type? UnwrapTask(Type returnType)
    {
        if (returnType == typeof(Task) || returnType == typeof(void))
            return null;

        return returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : returnType;
    }
}

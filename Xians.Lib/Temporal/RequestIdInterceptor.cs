using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Temporal;

/// <summary>
/// Carries the request ID of the message being handled from a workflow into the activities it
/// schedules, so activity logs carry it too. Activities cannot read the workflow's memo, so the ID
/// travels as an activity header and is restored into the activity's async context.
/// </summary>
internal sealed class RequestIdInterceptor : IWorkerInterceptor
{
    /// <summary>Activity header holding the request ID.</summary>
    internal const string HeaderKey = "xians-request-id";

    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
        new WorkflowInbound(nextInterceptor);

    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) =>
        new ActivityInbound(nextInterceptor);

    /// <summary>The request ID header for an activity scheduled now, or the headers unchanged.</summary>
    internal static IDictionary<string, Payload>? WithRequestId(IDictionary<string, Payload>? headers, string? requestId, Func<string, Payload> toPayload)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return headers;

        var withId = headers == null ? new Dictionary<string, Payload>() : new Dictionary<string, Payload>(headers);
        withId[HeaderKey] = toPayload(requestId);
        return withId;
    }

    private sealed class WorkflowInbound : WorkflowInboundInterceptor
    {
        public WorkflowInbound(WorkflowInboundInterceptor next) : base(next) { }

        public override void Init(WorkflowOutboundInterceptor outbound) => base.Init(new WorkflowOutbound(outbound));
    }

    private sealed class WorkflowOutbound : WorkflowOutboundInterceptor
    {
        public WorkflowOutbound(WorkflowOutboundInterceptor next) : base(next) { }

        public override Task<TResult> ScheduleActivityAsync<TResult>(ScheduleActivityInput input) =>
            base.ScheduleActivityAsync<TResult>(input with
            {
                Headers = WithRequestId(input.Headers, XiansContext.SafeRequestId, Workflow.PayloadConverter.ToPayload)
            });

        public override Task<TResult> ScheduleLocalActivityAsync<TResult>(ScheduleLocalActivityInput input) =>
            base.ScheduleLocalActivityAsync<TResult>(input with
            {
                Headers = WithRequestId(input.Headers, XiansContext.SafeRequestId, Workflow.PayloadConverter.ToPayload)
            });
    }

    private sealed class ActivityInbound : ActivityInboundInterceptor
    {
        public ActivityInbound(ActivityInboundInterceptor next) : base(next) { }

        public override async Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            string? requestId = null;
            if (input.Headers != null && input.Headers.TryGetValue(HeaderKey, out var payload))
            {
                try
                {
                    requestId = ActivityExecutionContext.Current.PayloadConverter.ToValue(payload, typeof(string)) as string;
                }
                catch
                {
                    // A malformed header only costs the log tag; never fail the activity over it.
                }
            }

            // Changes to an AsyncLocal inside this async method stay in its flow, so the caller's
            // value is untouched once it returns.
            if (!string.IsNullOrWhiteSpace(requestId) && string.IsNullOrEmpty(XiansContext.GetRequestId()))
            {
                XiansContext.SetRequestId(requestId);
            }

            return await base.ExecuteActivityAsync(input);
        }
    }
}

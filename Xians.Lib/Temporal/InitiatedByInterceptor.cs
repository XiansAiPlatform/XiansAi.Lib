using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Temporal;

/// <summary>
/// Carries the X-On-Behalf-Of user from a workflow into the activities it schedules, via a Temporal
/// header, because activities cannot read their workflow's memo. Only feeds log attribution.
/// </summary>
public sealed class InitiatedByInterceptor : IWorkerInterceptor
{
    internal const string HeaderName = "xians-initiated-by";

    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
        new WorkflowInbound(nextInterceptor);

    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) =>
        new ActivityInbound(nextInterceptor);

    internal static IDictionary<string, Payload>? AddHeader(
        IDictionary<string, Payload>? headers, string? initiatedBy, Func<string, Payload> toPayload)
    {
        if (string.IsNullOrWhiteSpace(initiatedBy))
            return headers;

        var result = headers == null
            ? new Dictionary<string, Payload>()
            : new Dictionary<string, Payload>(headers);
        result[HeaderName] = toPayload(initiatedBy);
        return result;
    }

    internal static string? ReadHeader(
        IReadOnlyDictionary<string, Payload>? headers, Func<Payload, string?> fromPayload)
    {
        if (headers == null || !headers.TryGetValue(HeaderName, out var payload))
            return null;
        try
        {
            var value = fromPayload(payload);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private sealed class WorkflowInbound : WorkflowInboundInterceptor
    {
        public WorkflowInbound(WorkflowInboundInterceptor next) : base(next) { }

        public override void Init(WorkflowOutboundInterceptor outbound) =>
            base.Init(new WorkflowOutbound(outbound));
    }

    private sealed class WorkflowOutbound : WorkflowOutboundInterceptor
    {
        public WorkflowOutbound(WorkflowOutboundInterceptor next) : base(next) { }

        public override Task<TResult> ScheduleActivityAsync<TResult>(ScheduleActivityInput input) =>
            base.ScheduleActivityAsync<TResult>(input with { Headers = WithInitiatedBy(input.Headers) });

        public override Task<TResult> ScheduleLocalActivityAsync<TResult>(ScheduleLocalActivityInput input) =>
            base.ScheduleLocalActivityAsync<TResult>(input with { Headers = WithInitiatedBy(input.Headers) });

        private static IDictionary<string, Payload>? WithInitiatedBy(IDictionary<string, Payload>? headers) =>
            AddHeader(headers, XiansContext.TryGetInitiatedBy(), value => Workflow.PayloadConverter.ToPayload(value));
    }

    private sealed class ActivityInbound : ActivityInboundInterceptor
    {
        public ActivityInbound(ActivityInboundInterceptor next) : base(next) { }

        public override async Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            var converter = ActivityExecutionContext.Current.PayloadConverter;
            XiansContext.SetInitiatedBy(ReadHeader(input.Headers, p => converter.ToValue(p, typeof(string)) as string));
            try
            {
                return await base.ExecuteActivityAsync(input);
            }
            finally
            {
                XiansContext.ClearInitiatedBy();
            }
        }
    }
}

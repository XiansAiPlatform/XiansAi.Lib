using System.Text.RegularExpressions;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Workflows;
using Xians.Lib.Common;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Central resolver for workflow metadata (TenantId, Agent, UserId, idPostfix, etc.).
/// Extracts values from search attributes, memo, workflow ID, or workflow description.
/// Works in both workflow and activity contexts; in activity context can fetch parent workflow
/// description via Temporal client for accurate metadata (avoids workflow ID timestamp pollution).
/// </summary>
internal static class WorkflowMetadataResolver
{
    /// <summary>
    /// Matches Temporal's appended timestamp suffix(s) on scheduled workflow IDs.
    /// Handles: -2026-02-17T13:31:53Z, -2026-02-17T22, or multiple -2026-02-17T22-2026-02-18T00-2026-02-18T02
    /// </summary>
    private static readonly Regex TemporalScheduledTimestampSuffixRegex =
        new(@"(?:-\d{4}-\d{2}-\d{2}T[\d:.Z]+)+$", RegexOptions.Compiled);

    #region Value extraction

    /// <summary>
    /// Extracts a string value from search attributes by key.
    /// </summary>
    public static string? GetValueFromSearchAttributes(SearchAttributeCollection? searchAttrs, string keyName)
    {
        if (searchAttrs == null) return null;
        try
        {
            var key = SearchAttributeKey.CreateKeyword(keyName);
            return searchAttrs.Get(key)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a string value from workflow memo (IEncodedRawValue values).
    /// </summary>
    public static string? GetValueFromMemo(IReadOnlyDictionary<string, IEncodedRawValue>? memo, string keyName)
    {
        if (memo == null || !memo.TryGetValue(keyName, out var value)) return null;
        try
        {
            return value.Payload.Data.ToStringUtf8()?.Replace("\"", "");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses idPostfix from workflow ID. Format: {tenantId}:{agentName}:{workflowName}:{idPostfix}
    /// For scheduled workflows, strips Temporal's appended timestamp (e.g. -2026-02-17T13:31:53Z).
    /// </summary>
    public static string? ParseIdPostfixFromWorkflowId(string workflowId)
    {
        if (string.IsNullOrEmpty(workflowId)) return null;
        try
        {
            var parts = workflowId.Split(':');
            if (parts.Length < 4) return null;

            var idPart = parts[3];
            if (string.IsNullOrEmpty(idPart)) return null;

            var withoutTimestamp = TemporalScheduledTimestampSuffixRegex.Replace(idPart, string.Empty);
            return withoutTimestamp.Length > 0 ? withoutTimestamp : idPart;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Workflow context resolution

    /// <summary>
    /// Gets workflow ID from current context (workflow or activity).
    /// </summary>
    public static string GetWorkflowId()
    {
        if (Workflow.InWorkflow) return Workflow.Info.WorkflowId;
        if (ActivityExecutionContext.HasCurrent) return ActivityExecutionContext.Current.Info.WorkflowId;
        throw new InvalidOperationException("Not in workflow or activity context.");
    }

    /// <summary>
    /// Gets workflow run ID from current context.
    /// </summary>
    public static string GetWorkflowRunId()
    {
        if (Workflow.InWorkflow) return Workflow.Info.RunId;
        if (ActivityExecutionContext.HasCurrent) return ActivityExecutionContext.Current.Info.WorkflowRunId;
        throw new InvalidOperationException("Not in workflow or activity context.");
    }

    /// <summary>
    /// Resolves a metadata value from workflow context (search attrs, memo). Does not parse workflow ID.
    /// </summary>
    public static string? GetFromWorkflowContext(string keyName)
    {
        var fromSearchAttrs = GetFromSearchAttributes(keyName);
        if (!string.IsNullOrEmpty(fromSearchAttrs)) return fromSearchAttrs;

        return GetFromWorkflowMemo(keyName);
    }

    private static string? GetFromSearchAttributes(string keyName)
    {
        try
        {
            if (Workflow.InWorkflow)
                return GetValueFromSearchAttributes(Workflow.TypedSearchAttributes, keyName);
        }
        catch { }
        return null;
    }

    private static string? GetFromWorkflowMemo(string keyName)
    {
        try
        {
            if (Workflow.InWorkflow && Workflow.Memo.TryGetValue(keyName, out var value))
                return value.Payload.Data.ToStringUtf8()?.Replace("\"", "");
        }
        catch { }
        return null;
    }

    #endregion

    #region Workflow description (activity + client)

    /// <summary>
    /// Fetches parent workflow description when in activity. Returns null if not in activity or fetch fails.
    /// </summary>
    public static async Task<WorkflowExecutionDescription?> FetchWorkflowDescriptionAsync(ITemporalClient client)
    {
        if (!ActivityExecutionContext.HasCurrent) return null;
        try
        {
            var workflowId = ActivityExecutionContext.Current.Info.WorkflowId;
            var runId = ActivityExecutionContext.Current.Info.WorkflowRunId;
            var handle = client.GetWorkflowHandle(workflowId, runId);
            return await handle.DescribeAsync();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a value from workflow description (TypedSearchAttributes, then Memo).
    /// </summary>
    public static string? GetFromDescription(WorkflowExecutionDescription? description, string keyName)
    {
        if (description == null) return null;

        var fromSearchAttrs = GetValueFromSearchAttributes(description.TypedSearchAttributes, keyName);
        if (!string.IsNullOrEmpty(fromSearchAttrs)) return fromSearchAttrs;

        return GetValueFromMemo(description.Memo, keyName);
    }

    /// <summary>
    /// Resolves search attributes for a child/sub-workflow to inherit from the parent.
    /// In workflow context: returns Workflow.TypedSearchAttributes directly.
    /// In activity context: fetches parent workflow description via client and returns TypedSearchAttributes
    /// (or builds from mined values if TypedSearchAttributes is empty).
    /// Outside workflow/activity: returns null (caller should build from context).
    /// </summary>
    /// <param name="tenantId">Tenant ID for the child workflow.</param>
    /// <param name="agentName">Agent name for the child workflow.</param>
    /// <param name="client">Temporal client. Required when in activity context to fetch parent metadata.</param>
    /// <returns>Search attributes to use when starting the child workflow, or null.</returns>
    public static async Task<SearchAttributeCollection?> ResolveSearchAttributesForChildAsync(
        string tenantId,
        string agentName,
        ITemporalClient? client)
    {
        if (Workflow.InWorkflow)
            return Workflow.TypedSearchAttributes;

        if (!ActivityExecutionContext.HasCurrent || client == null)
            return null;

        var description = await FetchWorkflowDescriptionAsync(client);
        if (description?.TypedSearchAttributes != null)
            return description.TypedSearchAttributes;

        var userId = GetFromDescription(description, WorkflowConstants.Keys.UserId) ?? string.Empty;
        var idPostfix = GetFromDescription(description, WorkflowConstants.Keys.idPostfix) ?? string.Empty;
        return BuildSearchAttributes(tenantId, agentName, userId, idPostfix);
    }

    /// <summary>
    /// Resolves idPostfix, preferring workflow description when in activity with client provided.
    /// Falls back to sync resolution (search attrs, memo, workflow ID parsing).
    /// </summary>
    public static async Task<string?> ResolveIdPostfixAsync(ITemporalClient? client = null)
    {
        if (!Workflow.InWorkflow && !ActivityExecutionContext.HasCurrent) return null;

        if (client != null && ActivityExecutionContext.HasCurrent)
        {
            try
            {
                var description = await FetchWorkflowDescriptionAsync(client);
                var fromDescription = GetFromDescription(description, WorkflowConstants.Keys.idPostfix);
                if (!string.IsNullOrEmpty(fromDescription)) return fromDescription;
            }
            catch { /* fall through */ }
        }

        return ResolveIdPostfixSync();
    }

    /// <summary>
    /// Resolves idPostfix synchronously from workflow context (search attrs, memo) or workflow ID parsing.
    /// </summary>
    public static string? ResolveIdPostfixSync()
    {
        var fromContext = GetFromWorkflowContext(WorkflowConstants.Keys.idPostfix);
        if (!string.IsNullOrEmpty(fromContext)) return fromContext;

        if (Workflow.InWorkflow || ActivityExecutionContext.HasCurrent)
            return ParseIdPostfixFromWorkflowId(GetWorkflowId());

        return null;
    }

    #endregion

    #region Search attribute collection building

    /// <summary>
    /// Standard metadata keys for extraction/serialization.
    /// </summary>
    public static readonly string[] StandardMetadataKeys =
    [
        WorkflowConstants.Keys.TenantId,
        WorkflowConstants.Keys.Agent,
        WorkflowConstants.Keys.UserId,
        WorkflowConstants.Keys.idPostfix
    ];

    /// <summary>
    /// Builds SearchAttributeCollection from individual values.
    /// </summary>
    public static SearchAttributeCollection BuildSearchAttributes(
        string tenantId,
        string agentName,
        string userId,
        string idPostfix)
    {
        return new SearchAttributeCollection.Builder()
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.TenantId), tenantId)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.Agent), agentName)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.UserId), userId)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.idPostfix), idPostfix)
            .ToSearchAttributeCollection();
    }

    /// <summary>
    /// Extracts standard metadata to a serializable dictionary for passing through activities.
    /// </summary>
    public static Dictionary<string, object>? ExtractToSerializableDictionary(
        SearchAttributeCollection? searchAttributes,
        params string[] keys)
    {
        if (searchAttributes == null) return null;

        var result = new Dictionary<string, object>();
        foreach (var keyName in keys.Length > 0 ? keys : StandardMetadataKeys)
        {
            var value = GetValueFromSearchAttributes(searchAttributes, keyName);
            if (value != null) result[keyName] = value;
        }
        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Reconstructs SearchAttributeCollection from serializable dictionary.
    /// </summary>
    public static SearchAttributeCollection? ReconstructFromDictionary(Dictionary<string, object>? searchAttrs)
    {
        if (searchAttrs == null || searchAttrs.Count == 0) return null;

        var builder = new SearchAttributeCollection.Builder();
        foreach (var kvp in searchAttrs)
        {
            var key = SearchAttributeKey.CreateKeyword(kvp.Key);
            builder.Set(key, kvp.Value?.ToString() ?? string.Empty);
        }
        return builder.ToSearchAttributeCollection();
    }

    #endregion
}

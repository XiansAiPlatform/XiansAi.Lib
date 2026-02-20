using Microsoft.Extensions.Logging;
using Xians.Lib.Http;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Agents.Knowledge;

/// <summary>
/// Manages knowledge operations for a specific agent.
/// Provides methods to retrieve, update, delete, and list knowledge items.
/// All operations are automatically scoped to the agent's name and tenant.
/// REFACTORED: Uses KnowledgeActivityExecutor for context-aware execution.
/// </summary>
public class KnowledgeCollection
{
    private readonly XiansAgent _agent;
    private readonly KnowledgeActivityExecutor _executor;
    private readonly ILogger<KnowledgeCollection> _logger;
    private readonly List<Xians.Lib.Agents.Knowledge.Models.Knowledge> _localKnowledge = new();

    internal KnowledgeCollection(XiansAgent agent, IHttpClientService? httpService, Xians.Lib.Common.Caching.CacheService? cacheService)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<KnowledgeCollection>();
        
        // Initialize executor for context-aware execution
        var executorLogger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<KnowledgeActivityExecutor>();
        _executor = new KnowledgeActivityExecutor(agent, executorLogger);
    }

    /// <summary>
    /// Retrieves knowledge by name.
    /// Automatically scoped to this agent's tenant and agent name.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The knowledge object, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    public async Task<Xians.Lib.Agents.Knowledge.Models.Knowledge?> GetAsync(string knowledgeName, string? tenantId=null, CancellationToken cancellationToken = default)
    {
        tenantId = tenantId ?? GetTenantId();

        // Validate parameters first
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);

        var idPostfix = XiansContext.TryGetIdPostfix();
        
        _logger.LogDebug(
            "Getting knowledge '{Name}' for agent '{AgentName}' with tenantId '{TenantId}' and idPostfix '{IdPostfix}'",
            knowledgeName,
            _agent.Name,
            tenantId,
            idPostfix);

        // Context-aware execution via executor
        var knowledge = await _executor.GetAsync(knowledgeName, _agent.Name, tenantId, idPostfix, cancellationToken);

        // If the server returned system-scoped knowledge for a tenant-scoped
        // agent (e.g., template deployed previously but tenant copy missing),
        // create a tenant-scoped replica on the fly to keep scoping correct.
        if (knowledge != null && !_agent.SystemScoped && knowledge.SystemScoped)
        {
            await UpdateAsync(
                knowledgeName,
                knowledge.Content,
                knowledge.Type,
                systemScoped: false,
                description: knowledge.Description,
                visible: knowledge.Visible,
                cancellationToken);

            knowledge = await _executor.GetAsync(knowledgeName, _agent.Name, tenantId, idPostfix, cancellationToken);
        }

        return knowledge;
    }

    /// <summary>
    /// Retrieves system-scoped knowledge by name.
    /// Always uses SystemScoped=true and no tenant ID.
    /// </summary>
    internal async Task<Xians.Lib.Agents.Knowledge.Models.Knowledge?> GetSystemAsync(string knowledgeName, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);

        var idPostfix = XiansContext.TryGetIdPostfix();
        
        _logger.LogDebug(
            "Getting system knowledge '{Name}' for agent '{AgentName}'",
            knowledgeName,
            _agent.Name);

        return await _executor.GetSystemAsync(knowledgeName, _agent.Name, idPostfix, cancellationToken);
    }

    /// <summary>
    /// Updates or creates knowledge.
    /// Automatically scoped to this agent's tenant and agent name.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge.</param>
    /// <param name="content">The knowledge content.</param>
    /// <param name="type">Optional knowledge type (e.g., "instruction", "document", "json", "markdown").</param>
    /// <param name="systemScoped">Whether this knowledge is system-scoped. If null, uses the agent's SystemScoped setting.</param>
    /// <param name="description">Optional description of the knowledge item.</param>
    /// <param name="visible">Whether the knowledge item is visible. Defaults to true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the operation succeeds.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    internal async Task<bool> UpdateAsync(string knowledgeName, string content, string? type = null, bool? systemScoped = null, string? description = null, bool visible = true, CancellationToken cancellationToken = default)
    {
        // Validate parameters first
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);
        ValidationHelper.ValidateRequired(content, nameof(content));

        var tenantId = GetTenantId();
        var idPostfix = XiansContext.TryGetIdPostfix();
        
        // If systemScoped is not explicitly set, use the agent's SystemScoped setting
        var isSystemScoped = systemScoped ?? _agent.SystemScoped;
        
        _logger.LogDebug(
            "Updating knowledge '{Name}' for agent '{AgentName}', type '{Type}', systemScoped '{SystemScoped}'",
            knowledgeName,
            _agent.Name,
            type,
            isSystemScoped);

        // Context-aware execution via executor
        var result = await _executor.UpdateAsync(knowledgeName, content, type, _agent.Name, tenantId, isSystemScoped, idPostfix, description, visible, cancellationToken);
        
        // Track locally
        if (result)
        {
            var existingKnowledge = _localKnowledge.FirstOrDefault(k => k.Name == knowledgeName);
            if (existingKnowledge != null)
            {
                // Update existing
                existingKnowledge.Content = content;
                existingKnowledge.Type = type;
                existingKnowledge.SystemScoped = isSystemScoped;
                existingKnowledge.Description = description;
                existingKnowledge.Visible = visible;
            }
            else
            {
                // Add new
                _localKnowledge.Add(new Xians.Lib.Agents.Knowledge.Models.Knowledge
                {
                    Name = knowledgeName,
                    Content = content,
                    Type = type,
                    SystemScoped = isSystemScoped,
                    Agent = _agent.Name,
                    TenantId = tenantId,
                    Description = description,
                    Visible = visible
                });
            }
        }
        
        return result;
    }

    /// <summary>
    /// Deletes knowledge by name.
    /// Automatically scoped to this agent's tenant and agent name.
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    internal async Task<bool> DeleteAsync(string knowledgeName, CancellationToken cancellationToken = default)
    {
        // Validate parameters first
        ValidationHelper.ValidateRequiredWithMaxLength(knowledgeName, nameof(knowledgeName), 256);

        var tenantId = GetTenantId();
        var idPostfix = XiansContext.TryGetIdPostfix();
        
        _logger.LogDebug(
            "Deleting knowledge '{Name}' for agent '{AgentName}'",
            knowledgeName,
            _agent.Name);

        // Context-aware execution via executor
        var result = await _executor.DeleteAsync(knowledgeName, _agent.Name, tenantId, idPostfix, cancellationToken);
        
        // Remove from local tracking if deletion succeeded
        if (result)
        {
            var existingKnowledge = _localKnowledge.FirstOrDefault(k => k.Name == knowledgeName);
            if (existingKnowledge != null)
            {
                _localKnowledge.Remove(existingKnowledge);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Lists all knowledge for this agent.
    /// Automatically scoped to this agent's tenant and agent name.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of knowledge items.</returns>
    /// <exception cref="InvalidOperationException">Thrown if HTTP service is not available.</exception>
    public async Task<List<Xians.Lib.Agents.Knowledge.Models.Knowledge>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var idPostfix = XiansContext.TryGetIdPostfix();
        
        _logger.LogDebug(
            "Listing knowledge for agent '{AgentName}'",
            _agent.Name);

        // Context-aware execution via executor
        return await _executor.ListAsync(_agent.Name, tenantId, idPostfix, cancellationToken);
    }

    /// <summary>
    /// Displays a formatted summary of all locally tracked knowledge items.
    /// </summary>
    public Task DisplayKnowledgeSummaryAsync(CancellationToken cancellationToken = default)
    {
        if (_localKnowledge.Count == 0)
        {
            return Task.CompletedTask;
        }
        
        var knowledgeList = _localKnowledge;

        // Fixed box width for better console compatibility
        const int boxWidth = 63;
        
        // Display header
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"┌{new string('─', boxWidth)}┐");
        var header = $"│ UPLOADED KNOWLEDGE ({knowledgeList.Count})";
        Console.Write(header);
        Console.WriteLine($"{new string(' ', boxWidth - header.Length + 1)}│");
        Console.WriteLine($"├{new string('─', boxWidth)}┤");
        Console.ResetColor();
        Console.WriteLine();

        // Display each knowledge item
        for (int i = 0; i < knowledgeList.Count; i++)
        {
            var knowledge = knowledgeList[i];
            
            // Agent Name row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Agent:       ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(_agent.Name);
            
            // Knowledge Name row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Name:        ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(knowledge.Name);
            
            // Type row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Type:        ");
            Console.ResetColor();
            Console.WriteLine(knowledge.Type ?? "text");
            
            // Scope row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Scope:       ");
            Console.ForegroundColor = knowledge.SystemScoped ? ConsoleColor.Magenta : ConsoleColor.Blue;
            Console.WriteLine(knowledge.SystemScoped ? "System" : "Tenant");
            
            // Content Length row
            Console.Write("  ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Size:        ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{knowledge.Content?.Length ?? 0} chars");
            
            // Version row (if available)
            if (!string.IsNullOrEmpty(knowledge.Version))
            {
                Console.Write("  ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Version:     ");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(knowledge.Version);
            }
            
            Console.ResetColor();
            
            // Add spacing between knowledge items (except after the last one)
            if (i < knowledgeList.Count - 1)
            {
                Console.WriteLine();
            }
        }
        
        // Footer line
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"└{new string('─', boxWidth)}┘");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine();
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the tenant ID for cache and HTTP operations.
    /// For non-system-scoped agents, uses the agent's certificate tenant ID.
    /// For system-scoped agents, tries workflow context first, then falls back to certificate tenant ID.
    /// </summary>
    private string? GetTenantId()
    {
        // For non-system-scoped agents, use the agent's certificate tenant ID
        if (!_agent.SystemScoped)
        {
            return _agent.Options?.CertificateTenantId 
                ?? throw new InvalidOperationException(
                    "Tenant ID cannot be determined. XiansOptions must be properly configured with an API key.");
        }

        // System-scoped agent - no tenant ID needed
        return null;
    }
}


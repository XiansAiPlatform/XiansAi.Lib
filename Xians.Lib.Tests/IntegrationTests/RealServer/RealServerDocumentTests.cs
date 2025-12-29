using System.Text.Json;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Documents.Models;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server tests for Document Storage SDK.
/// These tests run against an actual Xians server to verify:
/// - Document CRUD operations
/// - Querying and filtering
/// - Key-based retrieval
/// - Bulk operations
/// 
/// Set SERVER_URL and API_KEY environment variables to run these tests.
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerDocuments")] // Force sequential execution
public class RealServerDocumentTests : RealServerTestBase, IDisposable
{
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private readonly string _agentName;
    private readonly List<string> _createdDocumentIds = new();
    
    public RealServerDocumentTests()
    {
        // Use unique agent name per test instance
        _agentName = $"DocumentTestAgent-{Guid.NewGuid().ToString()[..8]}";
    }

    public void Dispose()
    {
        // Cleanup created documents
        if (_platform != null && _agent != null && RunRealServerTests)
        {
            try
            {
                foreach (var id in _createdDocumentIds)
                {
                    try
                    {
                        _agent.Documents.DeleteAsync(id).GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clear the context to allow other tests to register agents
        try
        {
            XiansContext.Clear();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private async Task InitializePlatformAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var options = new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        _platform = await XiansPlatform.InitializeAsync(options);
        
        // Register non-system-scoped agent (documents require tenant scoping)
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = _agentName,
            SystemScoped = false  // Documents only work with tenant-scoped agents
        });
        
        // Define and upload workflow definition
        var workflow = _agent.Workflows.DefineBuiltIn("document-tests");
        await _agent.UploadWorkflowDefinitionsAsync();
        
        Console.WriteLine($"✓ Registered agent: {_agentName}");
        Console.WriteLine($"✓ Tenant ID: {_platform.Options.CertificateTenantId}");
    }

    #region Basic CRUD Tests

    [Fact]
    public async Task Document_SaveAndGet_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var document = new Document
            {
                Type = "test-data",
                Content = JsonSerializer.SerializeToElement(new
                {
                    Message = "Hello from document test",
                    Timestamp = DateTime.UtcNow,
                    Value = 42
                }),
                Metadata = new Dictionary<string, object>
                {
                    ["category"] = "integration-test",
                    ["priority"] = "high"
                }
            };

            // Act - Save
            var saved = await _agent!.Documents.SaveAsync(document);
            _createdDocumentIds.Add(saved.Id!);

            Assert.NotNull(saved.Id);
            Console.WriteLine($"  ✓ Document saved: {saved.Id}");

            // Act - Get
            var retrieved = await _agent!.Documents.GetAsync(saved.Id!);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(saved.Id, retrieved.Id);
            Assert.Equal("test-data", retrieved.Type);
            Assert.NotNull(retrieved.Content);
            
            var content = retrieved.Content!.Value;
            Assert.Equal("Hello from document test", content.GetProperty("Message").GetString());
            Assert.Equal(42, content.GetProperty("Value").GetInt32());
            
            Console.WriteLine("  ✓ Document retrieved successfully");
        }
        catch (Exception ex)
        {
            throw new Exception($"SaveAndGet test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_SaveWithKey_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var document = new Document
            {
                Type = "user-preferences",
                Key = $"user-{Guid.NewGuid()}",
                Content = JsonSerializer.SerializeToElement(new
                {
                    Theme = "dark",
                    Language = "en",
                    Notifications = true
                })
            };

            var options = new DocumentOptions
            {
                UseKeyAsIdentifier = true,
                Overwrite = true
            };

            // Act - Save with key as identifier
            var saved = await _agent!.Documents.SaveAsync(document, options);
            _createdDocumentIds.Add(saved.Id!);

            Console.WriteLine($"  ✓ Document saved with key: {document.Key}");

            // Act - Retrieve by key
            var retrieved = await _agent!.Documents.GetByKeyAsync("user-preferences", document.Key!);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(document.Key, retrieved.Key);
            Assert.Equal("user-preferences", retrieved.Type);
            
            Console.WriteLine("  ✓ Document retrieved by key successfully");
        }
        catch (Exception ex)
        {
            throw new Exception($"SaveWithKey test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_Update_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange - Create initial document
            var document = new Document
            {
                Type = "test-update",
                Content = JsonSerializer.SerializeToElement(new { Version = 1 })
            };

            var saved = await _agent!.Documents.SaveAsync(document);
            _createdDocumentIds.Add(saved.Id!);

            // Modify the document
            saved.Content = JsonSerializer.SerializeToElement(new { Version = 2, Updated = true });

            // Act - Update
            var updated = await _agent!.Documents.UpdateAsync(saved);

            Assert.True(updated);
            Console.WriteLine($"  ✓ Document updated: {saved.Id}");

            // Verify update
            var retrieved = await _agent!.Documents.GetAsync(saved.Id!);
            Assert.NotNull(retrieved);
            Assert.Equal(2, retrieved.Content!.Value.GetProperty("Version").GetInt32());
            Assert.True(retrieved.Content.Value.GetProperty("Updated").GetBoolean());
            Assert.NotNull(retrieved.UpdatedAt);
            
            Console.WriteLine("  ✓ Update verified");
        }
        catch (Exception ex)
        {
            throw new Exception($"Update test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_Delete_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var document = new Document
            {
                Type = "test-delete",
                Content = JsonSerializer.SerializeToElement(new { ToDelete = true })
            };

            var saved = await _agent!.Documents.SaveAsync(document);

            // Act - Delete
            var deleted = await _agent!.Documents.DeleteAsync(saved.Id!);

            // Assert
            Assert.True(deleted);
            Console.WriteLine($"  ✓ Document deleted: {saved.Id}");

            // Verify deletion
            var retrieved = await _agent!.Documents.GetAsync(saved.Id!);
            Assert.Null(retrieved);
            
            Console.WriteLine("  ✓ Deletion verified");
        }
        catch (Exception ex)
        {
            throw new Exception($"Delete test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_Exists_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var document = new Document
            {
                Type = "test-exists",
                Content = JsonSerializer.SerializeToElement(new { Test = true })
            };

            var saved = await _agent!.Documents.SaveAsync(document);
            _createdDocumentIds.Add(saved.Id!);

            // Act & Assert - Should exist
            var exists = await _agent!.Documents.ExistsAsync(saved.Id!);
            Assert.True(exists);
            Console.WriteLine($"  ✓ Document exists: {saved.Id}");

            // Delete and check again
            await _agent!.Documents.DeleteAsync(saved.Id!);
            _createdDocumentIds.Remove(saved.Id!);
            
            var existsAfterDelete = await _agent!.Documents.ExistsAsync(saved.Id!);
            Assert.False(existsAfterDelete);
            
            Console.WriteLine("  ✓ Document no longer exists after deletion");
        }
        catch (Exception ex)
        {
            throw new Exception($"Exists test failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task Document_Query_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange - Create multiple documents
            var testType = $"query-test-{Guid.NewGuid().ToString()[..8]}";
            
            for (int i = 1; i <= 3; i++)
            {
                var doc = new Document
                {
                    Type = testType,
                    Content = JsonSerializer.SerializeToElement(new { Index = i }),
                    Metadata = new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["category"] = "test"
                    }
                };

                var saved = await _agent!.Documents.SaveAsync(doc);
                _createdDocumentIds.Add(saved.Id!);
                Console.WriteLine($"  ✓ Created test document {i}");
            }

            // Wait for indexing
            await Task.Delay(500);

            // Act - Query by type
            var query = new DocumentQuery
            {
                Type = testType,
                Limit = 10
            };

            var results = await _agent!.Documents.QueryAsync(query);

            // Assert
            Assert.NotNull(results);
            Assert.True(results.Count >= 3, $"Expected at least 3 documents, got {results.Count}");
            Assert.All(results, doc => Assert.Equal(testType, doc.Type));
            
            Console.WriteLine($"  ✓ Query returned {results.Count} documents");
        }
        catch (Exception ex)
        {
            throw new Exception($"Query test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_QueryWithFilters_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var testType = $"filter-test-{Guid.NewGuid().ToString()[..8]}";
            var now = DateTime.UtcNow;

            // Create documents with different metadata
            var documents = new[]
            {
                new Document
                {
                    Type = testType,
                    Content = JsonSerializer.SerializeToElement(new { Name = "Doc1" }),
                    Metadata = new Dictionary<string, object> { ["priority"] = "high", ["status"] = "active" }
                },
                new Document
                {
                    Type = testType,
                    Content = JsonSerializer.SerializeToElement(new { Name = "Doc2" }),
                    Metadata = new Dictionary<string, object> { ["priority"] = "low", ["status"] = "active" }
                },
                new Document
                {
                    Type = testType,
                    Content = JsonSerializer.SerializeToElement(new { Name = "Doc3" }),
                    Metadata = new Dictionary<string, object> { ["priority"] = "high", ["status"] = "inactive" }
                }
            };

            foreach (var doc in documents)
            {
                var saved = await _agent!.Documents.SaveAsync(doc);
                _createdDocumentIds.Add(saved.Id!);
            }

            await Task.Delay(500); // Wait for indexing

            // Act - Query with metadata filters
            var query = new DocumentQuery
            {
                Type = testType,
                MetadataFilters = new Dictionary<string, object>
                {
                    ["priority"] = "high"
                },
                Limit = 10
            };

            var results = await _agent!.Documents.QueryAsync(query);

            // Assert - Should only return high priority documents
            Assert.NotNull(results);
            Assert.True(results.Count >= 2, $"Expected at least 2 high priority documents, got {results.Count}");
            
            Console.WriteLine($"  ✓ Query with filters returned {results.Count} documents");
        }
        catch (Exception ex)
        {
            throw new Exception($"QueryWithFilters test failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region Bulk Operations

    [Fact]
    public async Task Document_DeleteMany_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange - Create multiple documents
            var ids = new List<string>();
            
            for (int i = 1; i <= 3; i++)
            {
                var doc = new Document
                {
                    Type = "bulk-delete-test",
                    Content = JsonSerializer.SerializeToElement(new { Index = i })
                };

                var saved = await _agent!.Documents.SaveAsync(doc);
                ids.Add(saved.Id!);
                Console.WriteLine($"  ✓ Created document {i}: {saved.Id}");
            }

            // Act - Delete many
            var deletedCount = await _agent!.Documents.DeleteManyAsync(ids);

            // Assert
            Assert.Equal(3, deletedCount);
            Console.WriteLine($"  ✓ Deleted {deletedCount} documents");

            // Verify all deleted
            foreach (var id in ids)
            {
                var exists = await _agent!.Documents.ExistsAsync(id);
                Assert.False(exists);
            }
            
            Console.WriteLine("  ✓ All deletions verified");
        }
        catch (Exception ex)
        {
            throw new Exception($"DeleteMany test failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Document_LargeContent_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange - Create large content
            var largeArray = Enumerable.Range(1, 1000).Select(i => new { Id = i, Data = $"Item {i}" }).ToArray();
            
            var document = new Document
            {
                Type = "large-content",
                Content = JsonSerializer.SerializeToElement(largeArray)
            };

            // Act
            var saved = await _agent!.Documents.SaveAsync(document);
            _createdDocumentIds.Add(saved.Id!);

            var retrieved = await _agent!.Documents.GetAsync(saved.Id!);

            // Assert
            Assert.NotNull(retrieved);
            var retrievedArray = retrieved.Content!.Value.Deserialize<object[]>();
            Assert.Equal(1000, retrievedArray!.Length);
            
            Console.WriteLine($"  ✓ Large document (1000 items) saved and retrieved");
        }
        catch (Exception ex)
        {
            throw new Exception($"LargeContent test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_ComplexMetadata_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var document = new Document
            {
                Type = "complex-metadata",
                Content = JsonSerializer.SerializeToElement(new { Test = true }),
                Metadata = new Dictionary<string, object>
                {
                    ["string"] = "value",
                    ["number"] = 42,
                    ["boolean"] = true,
                    ["date"] = DateTime.UtcNow.ToString("O"),
                    ["array"] = new[] { 1, 2, 3 }
                }
            };

            // Act
            var saved = await _agent!.Documents.SaveAsync(document);
            _createdDocumentIds.Add(saved.Id!);

            var retrieved = await _agent!.Documents.GetAsync(saved.Id!);

            // Assert
            Assert.NotNull(retrieved);
            Assert.NotNull(retrieved.Metadata);
            Assert.Equal("value", retrieved.Metadata!["string"]);
            
            Console.WriteLine("  ✓ Complex metadata saved and retrieved");
        }
        catch (Exception ex)
        {
            throw new Exception($"ComplexMetadata test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_GetNonExistent_ReturnsNull()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Act
            var result = await _agent!.Documents.GetAsync("nonexistent-doc-12345");

            // Assert
            Assert.Null(result);
            Console.WriteLine("  ✓ Non-existent document returns null");
        }
        catch (Exception ex)
        {
            throw new Exception($"GetNonExistent test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_DeleteNonExistent_ReturnsFalse()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Act
            var result = await _agent!.Documents.DeleteAsync("nonexistent-doc-12345");

            // Assert
            Assert.False(result);
            Console.WriteLine("  ✓ Deleting non-existent document returns false");
        }
        catch (Exception ex)
        {
            throw new Exception($"DeleteNonExistent test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_UpdateNonExistent_ReturnsFalse()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var document = new Document
            {
                Id = "nonexistent-doc-12345",
                Type = "test",
                Content = JsonSerializer.SerializeToElement(new { Test = true })
            };

            // Act
            var result = await _agent!.Documents.UpdateAsync(document);

            // Assert
            Assert.False(result);
            Console.WriteLine("  ✓ Updating non-existent document returns false");
        }
        catch (Exception ex)
        {
            throw new Exception($"UpdateNonExistent test failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region TTL and Expiration Tests

    [Fact]
    public async Task Document_WithTTL_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var document = new Document
            {
                Type = "ttl-test",
                Content = JsonSerializer.SerializeToElement(new { Temporary = true })
            };

            var options = new DocumentOptions
            {
                TtlMinutes = 60 // 1 hour
            };

            // Act
            var saved = await _agent!.Documents.SaveAsync(document, options);
            _createdDocumentIds.Add(saved.Id!);

            // Assert - Document should have expiration time
            var retrieved = await _agent!.Documents.GetAsync(saved.Id!);
            Assert.NotNull(retrieved);
            Assert.NotNull(retrieved.ExpiresAt);
            
            // ExpiresAt should be approximately 1 hour from now
            var expectedExpiry = DateTime.UtcNow.AddMinutes(60);
            var expiryDiff = Math.Abs((retrieved.ExpiresAt!.Value - expectedExpiry).TotalMinutes);
            Assert.True(expiryDiff < 2, $"Expiry time difference: {expiryDiff} minutes");
            
            Console.WriteLine($"  ✓ Document has TTL: {retrieved.ExpiresAt:O}");
        }
        catch (Exception ex)
        {
            throw new Exception($"TTL test failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Document_UseKeyAsIdentifier_RequiresTypeAndKey()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange - Document missing Type
            var document = new Document
            {
                Key = "test-key",
                Content = JsonSerializer.SerializeToElement(new { Test = true })
            };

            var options = new DocumentOptions
            {
                UseKeyAsIdentifier = true
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _agent!.Documents.SaveAsync(document, options);
            });

            Console.WriteLine("  ✓ UseKeyAsIdentifier validation works");
        }
        catch (Exception ex)
        {
            throw new Exception($"Validation test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task Document_UpdateWithoutId_ThrowsException()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange - Document without ID
            var document = new Document
            {
                Type = "test",
                Content = JsonSerializer.SerializeToElement(new { Test = true })
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _agent!.Documents.UpdateAsync(document);
            });

            Console.WriteLine("  ✓ Update without ID throws exception");
        }
        catch (Exception ex)
        {
            throw new Exception($"UpdateWithoutId test failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region System-Scoped Agent Tests

    [Fact]
    public async Task Document_SystemScopedAgent_OutsideWorkflow_ThrowsException()
    {
        if (!RunRealServerTests) return;

        // Create a system-scoped agent
        var options = new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        var platform = await XiansPlatform.InitializeAsync(options);
        var systemAgent = platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = $"SystemScopedTest-{Guid.NewGuid().ToString()[..8]}",
            SystemScoped = true
        });

        try
        {
            var document = new Document
            {
                Type = "test",
                Content = JsonSerializer.SerializeToElement(new { Test = true })
            };

            // Act & Assert - Should throw when called outside workflow context
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await systemAgent.Documents.SaveAsync(document);
            });

            Console.WriteLine("  ✓ System-scoped agent correctly requires workflow context");
        }
        finally
        {
            XiansContext.Clear();
        }
    }

    #endregion
}

/// <summary>
/// Test collection to force sequential execution of document tests.
/// </summary>
[CollectionDefinition("RealServerDocuments", DisableParallelization = true)]
public class RealServerDocumentsCollection
{
}


namespace XiansAi.Memory;

/// <summary>
/// Provides document storage operations for AI agents to save, retrieve, and manage JSON documents.
/// </summary>
public interface IDocumentStore
{
    /// <summary>
    /// Saves a document to the database.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="document">The document to save.</param>
    /// <param name="options">Optional storage options like TTL.</param>
    /// <returns>The saved document with generated ID if new.</returns>
    Task<Document> SaveAsync(Document document, DocumentOptions? options = null);

    /// <summary>
    /// Retrieves a document by its ID.
    /// </summary>
    /// <typeparam name="T">The expected type of the document content.</typeparam>
    /// <param name="id">The document ID.</param>
    /// <returns>The document if found, null otherwise.</returns>
    Task<Document?> GetAsync(string id);

    /// <summary>
    /// Retrieves a document by its type and custom key combination.
    /// </summary>
    /// <typeparam name="T">The expected type of the document content.</typeparam>
    /// <param name="type">The document type.</param>
    /// <param name="key">The custom key.</param>
    /// <returns>The document if found, null otherwise.</returns>
    Task<Document?> GetByKeyAsync(string type, string key);

    /// <summary>
    /// Queries documents based on metadata filters.
    /// </summary>
    /// <typeparam name="T">The expected type of the document content.</typeparam>
    /// <param name="query">The query parameters.</param>
    /// <returns>A list of matching documents.</returns>
    Task<List<Document>> QueryAsync(DocumentQuery query);

    /// <summary>
    /// Updates an existing document.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="document">The document to update with its ID.</param>
    /// <returns>True if updated, false if not found.</returns>
    Task<bool> UpdateAsync(Document document);

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    /// <param name="id">The document ID to delete.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// Deletes multiple documents by their IDs.
    /// </summary>
    /// <param name="ids">The document IDs to delete.</param>
    /// <returns>The number of documents deleted.</returns>
    Task<int> DeleteManyAsync(IEnumerable<string> ids);

    /// <summary>
    /// Checks if a document exists.
    /// </summary>
    /// <param name="id">The document ID to check.</param>
    /// <returns>True if exists, false otherwise.</returns>
    Task<bool> ExistsAsync(string id);
}

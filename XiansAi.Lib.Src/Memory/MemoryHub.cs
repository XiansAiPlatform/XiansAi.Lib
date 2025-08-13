using Server;

namespace XiansAi.Memory;

public static class MemoryHub
{
    private static readonly IObjectCache _cache = new ObjectCache();
    private static readonly IDocumentStore _documents = new DocumentStore();

    public static IObjectCache Cache => _cache;
    public static IDocumentStore Documents => _documents;

}

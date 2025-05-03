using Server;

namespace XiansAi.Memory;

public interface IMemoryHub
{
    IObjectCache Cache { get; }
}

public class MemoryHub : IMemoryHub
{
    public readonly IObjectCache _cache;

    public MemoryHub()
    {
        _cache = new ObjectCache();
    }

    public IObjectCache Cache => _cache;

}

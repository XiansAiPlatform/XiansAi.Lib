
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DockerImageAttribute: Attribute
{
    public string Name { get; private set; }

    public DockerImageAttribute(string name) {
        Name = name;
    }

}

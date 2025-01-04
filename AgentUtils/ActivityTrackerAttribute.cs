
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ActivityTrackerAttribute: Attribute
{
    public string Name { get; private set; }

    public ActivityTrackerAttribute(string name) {
        Name = name;
    }

}

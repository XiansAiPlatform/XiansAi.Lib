namespace XiansAi.Flow;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CategoriesAttribute: Attribute
{
    public string[] Categories { get; private set; } = [];

    public CategoriesAttribute(params string[] categories) {
        Categories = categories;
    }
}

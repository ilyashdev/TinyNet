namespace TinyNet.Middlewares;
[AttributeUsage(AttributeTargets.Class)]
public class FilterAttribute : Attribute
{
    public Type FilterType;

    public FilterAttribute(Type filterType)
    {
        this.FilterType = filterType;
    }
}